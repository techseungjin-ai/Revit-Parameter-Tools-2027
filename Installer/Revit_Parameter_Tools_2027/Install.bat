@echo off
chcp 65001 > nul
title Revit 2027 매개변수 도구 애드인 자동 설치기
echo ========================================================
echo    Revit 2027 매개변수 도구 (복제/일괄생성 & 값복사) 설치
echo ========================================================
echo.

set "TARGET_DIR=%APPDATA%\Autodesk\Revit\Addins\2027"

if not exist "%TARGET_DIR%" (
    echo [안내] Revit 2027 애드인 폴더를 생성합니다: %TARGET_DIR%
    mkdir "%TARGET_DIR%" 2>nul
)

echo [1/2] 매개변수 복제 및 일괄생성 도구 설치 중...
xcopy /E /I /Y "%~dp0ParameterBatchManager" "%TARGET_DIR%\ParameterBatchManager" > nul
copy /Y "%~dp0ParameterBatchManager.addin" "%TARGET_DIR%\" > nul

echo [2/2] 매개변수 데이터 값 복사 도구 설치 중...
xcopy /E /I /Y "%~dp0ParameterValueCopier" "%TARGET_DIR%\ParameterValueCopier" > nul
copy /Y "%~dp0ParameterValueCopier.addin" "%TARGET_DIR%\" > nul

echo.
echo ========================================================
echo  [성공] 설치가 정상적으로 완료되었습니다!
echo  Revit 2027을 실행하시면 [매개변수 도구] 리본 탭에서
echo  두 가지 도구를 바로 사용하실 수 있습니다.
echo ========================================================
echo.
pause