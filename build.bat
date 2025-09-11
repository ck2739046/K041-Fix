if exist "bin\Release\net472\K041_Fix.dll" (
    del "bin\Release\net472\K041_Fix.dll"
)

if exist "K041_Fix.sln" (
    del "K041_Fix.sln"
)

dotnet build -c Release

if exist "bin\Release\net472\K041_Fix.dll" (
    copy "bin\Release\net472\K041_Fix.dll" "."
) else (
    pause
)
