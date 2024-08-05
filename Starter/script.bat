
SET DataPath=%APPDATA%\BCI\CalibrationFiles\Completed
SET OutputPath=%APPDATA%\BCI\CalibrationFiles
SET fileName1=%~1
SET fileName2=%~2
SET fileName3=%~3
SET fileName4=%~4
SET fileName5=%~5
SET outputFileName=%~6

REM E.g., cd "C:\Users\Sekhar1224\AppData\Roaming\BCI\CalibrationFiles\Completed"
pushd "%DataPath%"

copy "%~dp0HeaderTemplate.txt"/a + "%fileName1%.bin"/b "%fileName1%.dat"
copy "%~dp0HeaderTemplate.txt"/a + "%fileName2%.bin"/b "%fileName2%.dat"
copy "%~dp0HeaderTemplate.txt"/a + "%fileName3%.bin"/b "%fileName3%.dat"
copy "%~dp0HeaderTemplate.txt"/a + "%fileName4%.bin"/b "%fileName4%.dat"
copy "%~dp0HeaderTemplate.txt"/a + "%fileName5%.bin"/b "%fileName5%.dat"

REM E.g., cd "C:\Program Files\BCI"
popd

"%~dp0P300Classifier_electrode_selection.exe" -TrainingDataFiles "%DataPath%\%fileName1%.dat" "%DataPath%\%fileName2%.dat" "%DataPath%\%fileName3%.dat" "%DataPath%\%fileName4%.dat" "%DataPath%\%fileName5%.dat" -inicfg "%~dp0parameters_AAC-BCI.ini" -ClassifierOutputFile "%OutputPath%\%outputFileName%.coef"
