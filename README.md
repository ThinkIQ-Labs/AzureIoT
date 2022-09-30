# ThinkIQ Azure IoT Project #

## WHAT IS THE PROBLEM? ##

There is currently a divergence between IoT suppliers while the users are looking for convergence and interoperability. While Azure IoT Central is delivering capabilities that are attractive to a wide set of users, it is unlikely to reach the same smart manufacturing depth delivered by the ThinkIQ Manufacturing Platform and the CESMII Smart Manufacturing Innovation Platform (SMIP). 

## TECHNICAL APPROACH: ##

- The functionality will be implemented by connecting the ThinkIQ Manufacturing Platform or the CESMII Smart Manufacturing Innovation Platform (SMIP) to Azure IoT through the Azure IoT Central API.
- The ThinkIQ Platform and CESMII SMIP Information Model Editor and Model Storage will be enhanced to configure integration with Azure IoT Central.

## Terminology Mapping ##

![image](https://user-images.githubusercontent.com/114267160/193169026-142a5f90-3534-4f81-a83e-524f7099173b.png)

**Simulating Azure IoT Devices** <br> 
- To demo a fictitious trucking company that operates refrigerated trucks, created a utility to simulate the trucks (on apps.azureiotcentral.com)
   - Trucks (Devices) have optimal temperature, geo-location, states, and command properties 
- Azure IoT Central could also export the data, which would store the data on Azure Event Hub

**Connector** <br> 
- A connector was built and  on a SMIP test system.
- When running, the connector automatically detects the connection to Azure IoT Central. 
- Running the connector would connect the trucks (Azure IoT devices) to Azure IoT Central and begin sending truck state, cooling system state, content state, content temperature, location, and events.

**Reading from Azure IoT Central and Writing to SMIP**
- Creates a Library to store the objects
- Takes the Device Templates (from Azure IoT) and creates Equipment Types
- Reads the Trucks and creates instances of Trucks (as Equipment) on SMIP
- Detects and imports Trailers as composite equipment 



### Caveats ####
- This project was completed in mid-2022. Azure IoT Central APIs may have changed since this project was finalized. Users may encounter errors if Azure IoT Central APIs have changed. 

## Development Tools and Environment ##

This project is written in C# using Visual Studio 2019, .NET Standard 2.1 and .NET Core 3.1. It also requires access to Azure IoT Central.


