using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using ParameterBatchManager.Models;

namespace ParameterBatchManager.Services
{
    public class CreateParameterRequest
    {
        public string Name { get; set; } = string.Empty;
        public ForgeTypeId DataTypeId { get; set; } = null!;
        public ForgeTypeId GroupTypeId { get; set; } = null!;
        public bool IsInstance { get; set; } = true;
        public List<Category> Categories { get; set; } = new();
    }

    public class CreateResult
    {
        public int SuccessCount { get; set; }
        public int SkipCount { get; set; }
        public int ErrorCount { get; set; }
        public List<string> Messages { get; set; } = new();
    }

    public class ParameterService
    {
        public List<ProjectParameterModel> GetProjectParameters(Document doc)
        {
            var result = new List<ProjectParameterModel>();
            if (doc == null) return result;

            BindingMap bindingMap = doc.ParameterBindings;
            DefinitionBindingMapIterator iterator = bindingMap.ForwardIterator();
            iterator.Reset();

            while (iterator.MoveNext())
            {
                Definition definition = iterator.Key;
                ElementBinding binding = (ElementBinding)iterator.Current;

                var categories = new List<Category>();
                if (binding.Categories != null)
                {
                    foreach (Category cat in binding.Categories)
                    {
                        categories.Add(cat);
                    }
                }

                ForgeTypeId dataType = definition.GetDataType();
                ForgeTypeId groupType = definition.GetGroupTypeId();

                var dataTypeOption = ForgeCatalog.FindDataType(dataType);
                var groupOption = ForgeCatalog.FindGroup(groupType);

                result.Add(new ProjectParameterModel
                {
                    Name = definition.Name,
                    DataTypeId = dataType,
                    DataTypeName = dataTypeOption.DisplayName,
                    GroupTypeId = groupType,
                    GroupName = groupOption.DisplayName,
                    IsInstance = binding is InstanceBinding,
                    Definition = definition,
                    Categories = categories.OrderBy(c => c.Name).ToList()
                });
            }

            return result.OrderBy(p => p.Name).ToList();
        }

        public List<CategoryItem> GetAllBindableCategories(Document doc)
        {
            var result = new List<CategoryItem>();
            if (doc == null) return result;

            Categories categories = doc.Settings.Categories;
            foreach (Category cat in categories)
            {
                if (cat.AllowsBoundParameters)
                {
                    result.Add(new CategoryItem(cat));
                }
            }

            return result.OrderBy(c => c.Name).ToList();
        }

        public CreateResult CreateParameters(Document doc, List<CreateParameterRequest> requests, Action<string>? logger = null)
        {
            var result = new CreateResult();
            if (doc == null || requests == null || requests.Count == 0)
                return result;

            // Existing parameter names check
            var existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var iterator = doc.ParameterBindings.ForwardIterator();
            iterator.Reset();
            while (iterator.MoveNext())
            {
                existingNames.Add(iterator.Key.Name);
            }

            string tempFile = Path.Combine(Path.GetTempPath(), $"RevitTempSharedParam_{Guid.NewGuid():N}.txt");
            string originalSharedParamFile = string.Empty;
            try
            {
                originalSharedParamFile = doc.Application.SharedParametersFilename;
            }
            catch
            {
                // Can be empty or not set
            }

            try
            {
                // Write standard Revit shared parameter template
                string template = "# This is a Revit shared parameter file.\r\n" +
                                  "# Do not edit manually.\r\n" +
                                  "*META\tVERSION\tMINVERSION\r\n" +
                                  "META\t2\t1\r\n" +
                                  "*GROUP\tID\tNAME\r\n" +
                                  "GROUP\t1\tAutoBatchGroup\r\n" +
                                  "*PARAM\r\n";
                File.WriteAllText(tempFile, template, System.Text.Encoding.Unicode);

                doc.Application.SharedParametersFilename = tempFile;
                DefinitionFile defFile = doc.Application.OpenSharedParameterFile();
                if (defFile == null)
                {
                    string msg = "임시 공유 매개변수 파일을 열 수 없습니다.";
                    logger?.Invoke(msg);
                    result.Messages.Add(msg);
                    result.ErrorCount = requests.Count;
                    return result;
                }

                DefinitionGroup group = defFile.Groups.get_Item("AutoBatchGroup") ?? defFile.Groups.Create("AutoBatchGroup");

                using (Transaction trans = new Transaction(doc, "매개변수 일괄 생성/복제"))
                {
                    trans.Start();

                    foreach (var req in requests)
                    {
                        string trimmedName = req.Name?.Trim() ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(trimmedName))
                        {
                            result.SkipCount++;
                            logger?.Invoke("이름이 비어있는 항목을 건너뛰었습니다.");
                            continue;
                        }

                        if (existingNames.Contains(trimmedName))
                        {
                            result.SkipCount++;
                            string msg = $"[건너뜀] 이미 프로젝트에 존재하는 매개변수: '{trimmedName}'";
                            logger?.Invoke(msg);
                            result.Messages.Add(msg);
                            continue;
                        }

                        if (req.Categories == null || req.Categories.Count == 0)
                        {
                            result.ErrorCount++;
                            string msg = $"[실패] '{trimmedName}': 선택된 카테고리가 없습니다.";
                            logger?.Invoke(msg);
                            result.Messages.Add(msg);
                            continue;
                        }

                        try
                        {
                            // Create external definition
                            var opt = new ExternalDefinitionCreationOptions(trimmedName, req.DataTypeId)
                            {
                                UserModifiable = true
                            };
                            Definition def = group.Definitions.Create(opt);

                            // Setup categories
                            CategorySet catSet = doc.Application.Create.NewCategorySet();
                            foreach (var cat in req.Categories)
                            {
                                catSet.Insert(cat);
                            }

                            // Setup binding
                            Binding binding = req.IsInstance
                                ? (Binding)doc.Application.Create.NewInstanceBinding(catSet)
                                : (Binding)doc.Application.Create.NewTypeBinding(catSet);

                            bool inserted = doc.ParameterBindings.Insert(def, binding, req.GroupTypeId);
                            if (!inserted)
                            {
                                inserted = doc.ParameterBindings.ReInsert(def, binding, req.GroupTypeId);
                            }

                            if (inserted)
                            {
                                existingNames.Add(trimmedName);
                                result.SuccessCount++;
                                string msg = $"[성공] '{trimmedName}' 생성 완료 ({ (req.IsInstance ? "인스턴스" : "유형") }, {req.Categories.Count}개 카테고리)";
                                logger?.Invoke(msg);
                                result.Messages.Add(msg);
                            }
                            else
                            {
                                result.ErrorCount++;
                                string msg = $"[실패] '{trimmedName}' 바인딩 추가 실패";
                                logger?.Invoke(msg);
                                result.Messages.Add(msg);
                            }
                        }
                        catch (Exception ex)
                        {
                            result.ErrorCount++;
                            string msg = $"[오류] '{trimmedName}': {ex.Message}";
                            logger?.Invoke(msg);
                            result.Messages.Add(msg);
                        }
                    }

                    trans.Commit();
                }
            }
            catch (Exception ex)
            {
                string msg = $"[전체 오류] 매개변수 생성 작업 중 예외 발생: {ex.Message}";
                logger?.Invoke(msg);
                result.Messages.Add(msg);
            }
            finally
            {
                // Restore original shared parameter file
                try
                {
                    doc.Application.SharedParametersFilename = originalSharedParamFile;
                }
                catch
                {
                }

                // Delete temp file
                if (File.Exists(tempFile))
                {
                    try
                    {
                        File.Delete(tempFile);
                    }
                    catch
                    {
                    }
                }
            }

            return result;
        }
    }
}
