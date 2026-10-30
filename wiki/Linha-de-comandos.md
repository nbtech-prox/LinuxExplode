# Linha de comandos

A aplicação tem um modo sem janela, útil para scripts, servidores e para quem gosta de terminal.
Todos os comandos se chamam `LinuxExplode` (fora do Flatpak) ou
`flatpak run pt.linuxexplode.LinuxExplode` (dentro do Flatpak).

## Ver o sistema

```bash
LinuxExplode --cli info          # resumo do sistema
LinuxExplode --cli freq          # frequência por núcleo/thread e relógios da GPU
LinuxExplode --cli sensores      # todos os sensores hwmon
LinuxExplode --cli potencia      # consumo: CPU (RAPL) + GPUs, total medido
LinuxExplode --cli discos        # discos, partições e S.M.A.R.T.
LinuxExplode --cli placa         # links PCIe + temperaturas de chipset/memória/rede
LinuxExplode --cli usb           # dispositivos USB, velocidade e drivers
LinuxExplode --cli audio         # placas de som, fluxos PCM e xruns do PipeWire
LinuxExplode --cli alertas       # regras de aviso, como no cartão Alertas
```

## Medir

```bash
LinuxExplode --cli bench multi ram disco        # CPU + RAM + disco
LinuxExplode --cli bench multi --repeticoes 3   # 3 corridas: mediana e variação
LinuxExplode --cli bench single                 # uma thread
LinuxExplode --cli bench gpu                    # path tracer Vulkan (GPU dedicada)
LinuxExplode --cli bench gpu 1                  # escolhe a GPU pelo índice
LinuxExplode --cli bench rt                     # ray tracing por hardware
LinuxExplode --cli bench real                   # descomprimir + compilar
LinuxExplode --cli bench latmem                 # latência de memória de 4 KB a 256 MB
LinuxExplode --cli ranking                      # mediana das últimas 3, por alvo
```

## Registar e guardar

```bash
LinuxExplode --cli gravar 60                    # grava uma sessão de 60 s em CSV
LinuxExplode --cli fps [ficheiro]               # sessão do MangoHud
LinuxExplode --cli relatorio html               # relatório completo no home
LinuxExplode --cli relatorio json               # o mesmo, em JSON
```

## Stress e diagnóstico

```bash
LinuxExplode --cli stress cpu 60
LinuxExplode --cli stress ram 60
LinuxExplode --cli stress disco 120
LinuxExplode --cli stress gpu 60
LinuxExplode --cli stress tudo 300
LinuxExplode --cli ciclos cpu 60 --ciclos 5 --pausa 30
LinuxExplode --cli vram --limite 2048 --bloco 128
LinuxExplode --cli autoteste curto nvme0n1
LinuxExplode --cli autoteste parar nvme0n1
LinuxExplode --cli suspender
LinuxExplode --cli suspender testar --segundos 15
LinuxExplode --cli netbench servidor
LinuxExplode --cli netbench cliente 192.168.1.10 --mb 64
LinuxExplode --cli historico                   # histórico de resultados
LinuxExplode --cli historico --desde 2026-10-01 --ate 2026-10-31
```

## Saída JSON

Todos os comandos aceitam **`--json`** (ou `-j`). A resposta é sempre um envelope com os mesmos
campos — nada de texto solto no `stdout`:

```json
{
  "aplicacao": "LinuxExplode",
  "versao": "0.20.15",
  "comando": "potencia",
  "quando": "2026-10-30T18:12:03Z",
  "dados": { }
}
```

Exemplos com `jq`:

```bash
LinuxExplode --cli potencia --json | jq .dados.TotalW
LinuxExplode --cli bench multi --json | jq .dados.resultado.Score
LinuxExplode --cli sensores --json | jq '[.dados[] | select(.Tipo=="Temperatura")] | length'
LinuxExplode --cli placa --json | jq '.dados.reduzidos'
LinuxExplode --cli audio --json | jq '.dados.Xruns'
LinuxExplode --cli stress cpu 60 --json > ~/stress.json
```

> **Em linguagem simples:** o `--json` é para outros programas lerem. Se só queres ver, usa o comando
> sem `--json` — o texto já está em português.

## Notas

- O `--cli` sai antes de abrir a janela, por isso funciona em servidores sem ecrã.
- O texto do `--cli` é sempre em português, mesmo que a janela esteja noutro idioma.
- Os mesmos dados aparecem na interface: a CLI não inventa nem esconde nada.
