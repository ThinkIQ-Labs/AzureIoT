set config="Debug"
powershell ../Build/build.ps1 -Script .\build.cake -ScriptArgs '-configuration=%config%','-target=Build'

