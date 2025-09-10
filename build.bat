if exist "bin\Release\net472\K041_Fix.dll" (
    del "bin\Release\net472\K041_Fix.dll"
)

dotnet build -c Release

if exist "bin\Release\net472\K041_Fix.dll" (
    copy "bin\Release\net472\K041_Fix.dll" "."
) else (
    pause
)
