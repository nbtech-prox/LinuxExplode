using LinuxExplode.Core;
using Xunit;

namespace LinuxExplode.Tests;

/// <summary>
/// Garante que os três idiomas andam juntos: sem chaves a mais num mapa, sem traduções vazias, e
/// com a cultura certa em cada um (é o que faz os números saírem com vírgula em pt/es e ponto em en).
/// </summary>
public sealed class TestesIdioma
{
    [Fact]
    public void Os_dois_mapas_tem_exactamente_as_mesmas_chaves()
    {
        var es = Textos.Chaves(Idioma.Es).OrderBy(x => x, StringComparer.Ordinal).ToList();
        var en = Textos.Chaves(Idioma.En).OrderBy(x => x, StringComparer.Ordinal).ToList();

        Assert.NotEmpty(es);
        Assert.Equal(es, en);
    }

    [Fact]
    public void Nenhuma_traducao_esta_vazia_ou_igual_a_chave_sem_querer()
    {
        foreach (var idioma in new[] { Idioma.Es, Idioma.En })
        {
            foreach (var chave in Textos.Chaves(idioma))
            {
                var valor = Textos.Bruto(idioma, chave);
                Assert.False(string.IsNullOrWhiteSpace(valor), $"{idioma}: tradução vazia para «{chave}»");
            }
        }
    }

    [Fact]
    public void A_cultura_acompanha_o_idioma()
    {
        var original = Textos.Atual;
        try
        {
            Textos.Aplicar(Idioma.Pt);
            Assert.Equal("pt-PT", System.Globalization.CultureInfo.CurrentCulture.Name);
            Textos.Aplicar(Idioma.Es);
            Assert.Equal("es-ES", System.Globalization.CultureInfo.CurrentCulture.Name);
            Textos.Aplicar(Idioma.En);
            Assert.Equal("en-GB", System.Globalization.CultureInfo.CurrentCulture.Name);
        }
        finally
        {
            Textos.Aplicar(original);
        }
    }

    [Fact]
    public void Os_moldes_traduzem_e_formatam_com_a_cultura_do_idioma()
    {
        var original = Textos.Atual;
        try
        {
            Textos.Aplicar(Idioma.Pt);
            Assert.Equal("8 núcleos", Textos.TF("{0} núcleos", 8));

            Textos.Aplicar(Idioma.En);
            Assert.Equal("8 cores", Textos.TF("{0} núcleos", 8));

            Textos.Aplicar(Idioma.Es);
            Assert.Equal("8 núcleos", Textos.TF("{0} núcleos", 8));
        }
        finally
        {
            Textos.Aplicar(original);
        }
    }

    [Fact]
    public void Sem_traducao_devolve_o_portugues_em_vez_de_rebentar()
    {
        var original = Textos.Atual;
        try
        {
            Textos.Aplicar(Idioma.En);
            Assert.Equal("texto que não existe", Textos.T("texto que não existe"));
        }
        finally
        {
            Textos.Aplicar(original);
        }
    }
}
