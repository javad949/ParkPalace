using System;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Xunit;
using Xunit.Abstractions;

namespace ParkPalace.IntegrationTests;

public class InspectOrdersControllerIl
{
    private readonly ITestOutputHelper _output;

    public InspectOrdersControllerIl(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ReadStringToken()
    {
        var dllPath = @"d:\Antigravity\ParkPlace\dist\release_v1.0.4\api\ParkPalace.Api.dll";
        using var stream = File.OpenRead(dllPath);
        using var peReader = new PEReader(stream);
        var reader = peReader.GetMetadataReader();

        var userStringHandle = MetadataTokens.UserStringHandle(0x000285);
        var str = reader.GetUserString(userStringHandle);
        _output.WriteLine($"String at 0x70000285 is: '{str}'");
    }
}
