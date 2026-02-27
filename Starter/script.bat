@echo off
setlocal EnableDelayedExpansion

SET DataPath=%APPDATA%\BCI\CalibrationFiles\Completed
SET OutputPath=%APPDATA%\BCI\CalibrationFiles

REM ---------------------------------
REM First, capture output filename
REM ---------------------------------
SET outputFileName=%~1
SHIFT

REM ---------------------------------
REM Move to data folder
REM ---------------------------------
pushd "%DataPath%"

SET datFiles=

REM ---------------------------------
REM Loop through all remaining args
REM ---------------------------------
:loop
IF "%~1"=="" GOTO done

SET fileName=%~1
echo Processing %fileName%

copy "%~dp0HeaderTemplate.txt"/a + "%fileName%.bin"/b "%fileName%.dat"

SET datFiles=!datFiles! "%DataPath%\%fileName%.dat"

SHIFT
GOTO loop

:done

popd

REM ---------------------------------
REM Run classifier
REM ---------------------------------
"P300Classifier_electrode_selection.exe" -TrainingDataFiles %datFiles% -inicfg "%~dp0parameters_AAC-BCI.ini" -ClassifierOutputFile "%OutputPath%\%outputFileName%.coef"
