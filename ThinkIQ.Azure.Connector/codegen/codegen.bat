rem Get file https://github.com/microsoft/PowerPlatformConnectors/blob/master/certified-connectors/AzureIoTCentral/apiDefinition.swagger.json
set nswag="C:\Program Files (x86)\Rico Suter\NSwagStudio\Win\NSwag.exe"
%nswag% openapi2csclient /className:AzureIoTCentral /namespace:ThinkIQ.Azure.IoT.Central.Client /input:apiDefinition.swagger.json /output:..\ThinkIQ.Azure.IoT.Central.Client\Azure.IoT.Central.Client.cs
