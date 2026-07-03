using ETPrinter.Models;
using Xunit;

namespace ETPrinter.Tests;

public class MpModuleTests
{
    // HasText steuert, ob PrintService das Modul druckt (leere Module werden
    // uebersprungen). Jedes einzeln befuellte Feld muss HasText=true liefern,
    // sonst zeigt die Preview Inhalt, der Druck aber nichts (stiller Datenverlust).
    [Fact]
    public void HasText_EmptyModule_IsFalse()
    {
        var module = new MpModule();
        Assert.False(module.HasText);
    }

    [Theory]
    [InlineData("HeaderText")]
    [InlineData("NetAddress1")]
    [InlineData("NetAddress2")]
    [InlineData("CpuName")]
    public void HasText_AnySingleFieldFilled_IsTrue(string propertyName)
    {
        var module = new MpModule();
        typeof(MpModule).GetProperty(propertyName)!.SetValue(module, "X");

        Assert.True(module.HasText);
    }

    [Fact]
    public void HasText_OnlyAddressCellFilled_IsTrue()
    {
        var module = new MpModule
        {
            AddressCells = [new MpAddressCell { CellIndex = 0, Text = "E 0.0" }]
        };
        Assert.True(module.HasText);
    }
}
