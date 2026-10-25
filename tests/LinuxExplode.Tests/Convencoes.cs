using System.Globalization;
using System.Runtime.CompilerServices;
using Xunit;

// A aplicação corre em pt-PT (Program.Main fixa a cultura) e os testes têm de ver o mesmo: sem isto
// as asserções de formatação ("+66,7 %") falhavam por causa do locale da máquina de CI.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace LinuxExplode.Tests;

internal static class Convencoes
{
    [ModuleInitializer]
    internal static void Iniciar()
    {
        var pt = new CultureInfo("pt-PT");
        CultureInfo.DefaultThreadCurrentCulture = pt;
        CultureInfo.DefaultThreadCurrentUICulture = pt;
        CultureInfo.CurrentCulture = pt;
        CultureInfo.CurrentUICulture = pt;
    }
}
