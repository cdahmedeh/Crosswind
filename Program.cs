// See https://aka.ms/new-console-template for more information

using FSUIPC;

Console.WriteLine("Welcome To LegacySimBridge!");

try
{
    FSUIPCConnection.Open();
}
catch (FSUIPCException ex)
{
    Console.WriteLine(ex.Message);
}

if (FSUIPCConnection.IsOpen)
{
    Console.WriteLine("Connected to FSUIPC!");
}