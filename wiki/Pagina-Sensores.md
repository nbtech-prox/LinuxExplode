# Página Sensores

![Página Sensores](https://raw.githubusercontent.com/nbtech-prox/LinuxExplode/main/docs/wiki/pagina-sensores.png)

É a página do dia-a-dia: **está quente? As ventoinhas trabalham? Quanto gasta? Está a perder
desempenho?**

## O que vês

### Alertas

Um cartão no topo diz se algo está fora dos limites e deixa-te mudar os limites:

| Regra | Dispara quando | Limite de origem |
|---|---|---|
| CPU quente | temperatura do processador ≥ limite | 90 °C |
| GPU quente | temperatura da placa gráfica ≥ limite | 90 °C |
| Ventoinhas a zero | todas as ventoinhas a 0 RPM com a CPU a ≥ 65 °C | — |
| Pouco espaço | uma partição real com menos de X GB livres | 10 GB |

A caixa **Notificações do sistema** manda um aviso para o ambiente (no máximo um por regra a cada 15
minutos). Dentro do Flatpak o `notify-send` normalmente não existe e a app desactiva a opção — em vez
de falhar em silêncio.

### Ao vivo

Quatro gráficos dos últimos **60 segundos**, uma amostra por segundo: temperatura da CPU, da GPU,
potência e frequência média. A linha da temperatura passa a vermelho a partir dos 90 °C.

### Throttling

Compara a frequência efectiva com o máximo do processador e conta **episódios de queda** (frequência
15 % abaixo do máximo com o processador ocupado). Em AMD o firmware não publica contadores de
throttle e o cartão di-lo — em vez de mostrar um número inventado.

### Energia e custo

Mostra a energia gasta **nesta sessão**, a média em watts e uma projecção de custo por dia e por mês.
Escreve a tua tarifa (€/kWh) e a conta ajusta-se. A média inclui o tempo com a janela aberta, mesmo
com o PC parado — é dito no cartão.

Precisas de correr `sudo ./permitir-energia.sh` uma vez (ver [Instalar](Instalar)) para a app poder ler
os contadores da CPU.

### Sessão gravada

Grava CPU, GPU, potência e frequência **uma vez por segundo** num ficheiro CSV. No fim mostra o
resumo (média, mínimo, máximo) e quatro gráficos da sessão inteira. A escrita é incremental: fechar a
app a meio não perde o que já foi medido.

### Lista de sensores

Em baixo, todos os sensores que o kernel expõe, agrupados por chip:

- `CPU (k10temp)` — AMD; `CPU (coretemp)` — Intel
- `GPU (amdgpu)` / `GPU (Intel)` / `GPU (NVIDIA)`
- `Placa-mãe (NCT6687D)`, `Placa-mãe (IT87)`… — ventoinhas e tensões da placa
- `nvme`, `Chassis (ACPI)`, etc.

Os filtros no topo (**Todos**, **Temperatura**, **Ventoinha**, **Potência**, **Tensão**,
**Frequência**) mudam o que aparece. No filtro **Frequência**, os núcleos da CPU vêm primeiro e a GPU
depois; no filtro **Potência**, o cartão *Consumo do PC* aparece antes da lista.

## O que significam os números

- **°C** — graus Celsius. Abaixo de 60 °C é confortável; 80–90 °C é esforço; acima do limite da app, é
  sinal de alarme.
- **RPM** — rotações por minuto das ventoinhas. Zero com a máquina quente pode significar curva
  passiva (normal em algumas placas) ou ventoinha desligada.
- **W** — watts. O cartão de energia mede o **socket da CPU + GPUs**, não a tomada: falta a
  placa-mãe, os discos e as perdas da fonte.
- **V** — tensões da placa-mãe (12 V, 5 V, 3,3 V e as internas do processador).

> **Em linguagem simples:** se os números estiverem verdes e as ventoinhas a rodar, está tudo bem. Se
> um alerta aparecer, o cartão diz qual regra disparou e a que temperatura.

## O que podes fazer

- Mudar os limites de alerta e a tarifa da electricidade.
- Gravar uma sessão e vê-la depois.
- Filtrar por tipo de sensor.
- Ver o mesmo em texto: `LinuxExplode --cli sensores` ou `--cli potencia`.

## Limites honestos

- Só aparecem os sensores que o kernel conhece. Numa máquina virtual quase não há nenhum.
- O uso (%) e a VRAM da GPU só existem em AMD (`amdgpu`); em NVIDIA e Intel há nome e temperatura.
- Se a placa-mãe precisar de um módulo do kernel que não está carregado, as ventoinhas dela não
  aparecem. A app não carrega módulos — diz o que falta.
