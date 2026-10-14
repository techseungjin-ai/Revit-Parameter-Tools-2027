@echo off
chcp 65001 > nul
title Revit 2027 매개변수 도구 애드인 제거기
echo ========================================================
echo    Revit 2027 매개변수 도구 애드인 제거
echo ========================================================
echo.

set "TARGET_DIR=%APPDATA%\Autodesk\Revit\Addins\2027"

echo 애드인 파일을 삭제합니다...
if exist "%TARGET_DIR%\ParameterBatchManager.addin" del /F /Q "%TARGET_DIR%\ParameterBatchManager.addin"
if exist "%TARGET_DIR%\ParameterBatchManager" rmdir /S /Q "%TARGET_DIR%\ParameterBatchManager"

if exist "%TARGET_DIR%\ParameterValueCopier.addin" del /F /Q "%TARGET_DIR%\ParameterValueCopier.addin"
if exist "%TARGET_DIR%\ParameterValueCopier" rmdir /S /Q "%TARGET_DIR%\ParameterValueCopier"

echo.
echo ========================================================
echo  [완료] 애드인이 정상적으로 제거되었습니다.
echo ========================================================
echo.
pause