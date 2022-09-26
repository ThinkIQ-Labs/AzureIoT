///////////////////////////////////////////////////////////////////////////////
// PREPROCESSOR DIRECTIVES
///////////////////////////////////////////////////////////////////////////////
#load "../Build/utilities.cake"
#load "../Build/fileutil.cake"

///////////////////////////////////////////////////////////////////////////////
// ARGUMENTS
///////////////////////////////////////////////////////////////////////////////

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////

Setup(ctx =>
{
	// Executed BEFORE the first task.
	Information("Running tasks...");
});

Teardown(ctx =>
{
	// Executed AFTER the last task.
	Information("Finished running tasks.");
});

var outputBaseDir = System.IO.Path.GetFullPath("../Build/BuildOutput/");

///////////////////////////////////////////////////////////////////////////////
// TASKS
///////////////////////////////////////////////////////////////////////////////
Task("Build")
    .Does(() =>
{ 
	var solutionPath = System.IO.Path.GetFullPath("ThinkIQ.Azure.Connector.sln");
    Build(solutionPath);
    Package();
});

private void Package()
{
    var solutionDir = ".";
    var serviceProj = "ThinkIQ.Azure.ConnectorService";
    var serviceApp = System.IO.Path.Combine(outputBaseDir, serviceProj);
    
    // Copy from default build output
    var projectBuildOutput = System.IO.Path.Combine(solutionDir, serviceProj, "bin", configuration, "netcoreapp3.1");
    projectBuildOutput = System.IO.Path.GetFullPath(projectBuildOutput);
    SignOutputs(projectBuildOutput);
    PackageInstall(projectBuildOutput, serviceApp, new List<string>(), true);	   
}

void SignOutputs(string srcDir)
{
    var files = new string[]
    {
        System.IO.Path.Combine(srcDir, "ThinkIQ.Azure.Connector.Utils.dll"),
        System.IO.Path.Combine(srcDir, "ThinkIQ.Azure.ConnectorService.dll"),
        System.IO.Path.Combine(srcDir, "ThinkIQ.Azure.IoT.Central.Client.dll"),
        System.IO.Path.Combine(srcDir, "ThinkIQ.Azure.IoT.Connector.dll"),
        System.IO.Path.Combine(srcDir, "ThinkIQ.DataAccess.dll"),
        System.IO.Path.Combine(srcDir, "ThinkIQ.Azure.ConnectorService.exe")
    };

    SignFiles("../Build", files);
}

RunTarget(target);
