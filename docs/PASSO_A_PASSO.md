# Passo a passo — Vinho no Display IoT

O projeto **não depende de nenhuma requisição da coleção da Smart Lamp**. Tudo o que é preciso está em `fiware/Vinho_no_Display.postman_collection.json`. Só se pressupõe a stack FIWARE do professor (https://github.com/fabiocabrini/fiware-made-simple), que **não deve ser alterada**.

## Parte A — Ambiente (repita a cada sessão do Learner Lab)

1. **Inicie o Learner Lab** e ligue a EC2 (Ubuntu x86_64, `t3.small`; evite tipos ARM). Anote o **IP público atual** (ele muda a cada início).
2. **Security Group** (TCP, origem conforme sua rede). Libere só as portas usadas:

   | Porta | Para que | Obrigatória |
   |---|---|---|
   | 22 | SSH | sim |
   | 1026 | Orion (app e Postman) | sim |
   | 8666 | STH-Comet (app e Postman) | sim |
   | 1883 | MQTT (ESP32) | sim |
   | 4041 | IoT Agent — provisionamento pelo Postman | sim |
   | 7896 | IoT Agent HTTP — simular sem hardware | só se for simular |

3. **Stack FIWARE** (primeira vez): por SSH,
   ```
   git clone https://github.com/fabiocabrini/fiware-made-simple.git
   cd fiware-made-simple
   ./scripts/check-requirements.sh      # instala o Docker se faltar (se pedir, saia e entre de novo)
   docker compose up -d --wait
   ```
   Nas sessões seguintes basta `docker compose up -d --wait` (confirme com `docker ps`: 7 containers *healthy*).
4. **Atualize o IP em três lugares:**
   - Postman: variável `host` da coleção;
   - ESP32/Wokwi: `BROKER_MQTT` no sketch **ou**, sem regravar, digite `broker 54.x.x.x` no Serial Monitor (fica salvo na flash);
   - Aplicação: tela *Dispositivo → Servidor FIWARE* (ou `Fiware:Host` no `appsettings.json`).

## Parte B — Provisionamento no Postman (ambiente novo)

Importe a coleção e rode **nesta ordem, antes de ligar o ESP32/Wokwi**:

| Passo | Requisição | Resultado esperado |
|---|---|---|
| 0.1 a 0.3 | Health (Orion, IoT Agent, STH) | 200 nas três |
| 1.0 | Ver service groups | se já houver `apikey: TEF`, **pule o 1.1** |
| 1.1 | Service Group TEF | 201 (ou 409 se já existir: tudo bem) |
| 1.2 | Registrar dispositivo `vinho001` | **201** (comandos `alerta`, `config`, `testealerta`; atributos `temperature`, `humidity`, `luminosity`, `alarm`, `uptime` e limites) |
| 1.3 a 1.6 | 4 subscriptions do STH (uma por atributo) | 201 cada |
| 1.7 | Conferir subscriptions | **exatamente 4** (se houver mais, apague as duplicadas por `DELETE /v2/subscriptions/{id}`) |

> **Importante:** o 1.2 inclui `"apikey": "TEF"`. Sem ele o IoT Agent não associa as leituras ao dispositivo e cria um segundo `vinho001` (`Thing:vinho001`). Confira no 1.2b que existe **só 1** dispositivo (`count: 1`).
>
> **Ordem importa:** se o ESP32 publicar antes do 1.2, o IoT Agent cria sozinho a entidade `Thing:vinho001` (sem atributos nem comandos) e o 1.2 passa a falhar. Para corrigir: pare o ESP32, rode 5.1 e 5.3 e repita o 1.2.
> **Nunca rode a pasta 5 inteira** (ela apaga o dispositivo).

Teste **sem hardware**: pasta 2 (2.3 limites, depois 2.1 leitura; repita o 2.1 — o `uptime` muda sozinho). Confira em **3.3** (valores atuais) e **4.4** (histórico do `alarm`). Em **1.7**, as subscriptions devem mostrar `timesSent` e `lastSuccess`.

## Parte C — ESP32

| Componente | Pino |
|---|---|
| DHT11 | GPIO 4 (VCC 3V3) |
| LDR | GPIO 32 |
| OLED SSD1306 | SDA 21, SCL 22 |
| Buzzer | GPIO 19 |
| LED RGB cátodo comum | 25 (R), 26 (G), 27 (B), com 220 Ω |
| Botão | GPIO 15 e GND |

- **Hardware real:** Arduino IDE, placa ESP32, bibliotecas Adafruit GFX, Adafruit SSD1306, DHT sensor library, PubSubClient. Abra `firmware/VinhoNoDisplay/VinhoNoDisplay.ino`, ajuste Wi-Fi e `BROKER_MQTT`, grave e abra o Serial Monitor a 115200.
- **Wokwi:** cole `wokwi/diagram.json`, `wokwi/sketch.ino` e `wokwi/libraries.txt`. Wi-Fi `Wokwi-GUEST` sem senha. Usa DHT22 (o Wokwi não tem DHT11).
- No Serial deve aparecer `MQTT conectado` e, a cada 5 s, `Publicado: a|0|t|..|h|..|l|..|u|..`. Se não conectar, digite `broker IP_DA_EC2`.
- **Botão:** toque curto troca de tela; toque longo (≥ 1,2 s) silencia o buzzer.

## Parte D — Aplicação ASP.NET

1. Instale o **.NET 8 SDK**.
2. Em `web/VinhoNoDisplay` rode `dotnet run` e abra a URL exibida (ex.: `https://localhost:49944`).
3. Em *Dispositivo → Servidor FIWARE* informe o IP da EC2 e clique em **Salvar e testar**.
4. Roteiro de verificação:
   - **Painel:** status *Online*, KPIs, 4 gráficos e o cartão *Supervisão* (médias, última ação);
   - **Perfis:** *Criar perfis sugeridos*, editar e excluir; **Garrafas:** cadastrar um vinho;
   - **Mensagens IoT:** *Consultar* (filtros de data e "Só anomalias");
   - **Dispositivo:** *Enviar configuração* e *Testar alerta*.
5. **A aplicação precisa ficar rodando** para o supervisor enviar os alertas.

## Parte E — Demonstração dos alertas

Veja o roteiro da seção 9 do `README.md`. Atalhos: Postman 3.4 a 3.7 (`alerta|1/2/4/0`) e o botão *Testar alerta*.

## Problemas comuns

| Sintoma | O que verificar |
|---|---|
| App: "Não foi possível falar com o FIWARE" | IP em *Dispositivo → Servidor FIWARE*; portas 1026 e 8666 |
| Painel "Sem sinal" | ESP32 publicando? `uptime` mudando no 3.3? IP do broker correto? Wokwi a 4 % pode levar minutos |
| 3.3 só mostra `config` e `testealerta` | Leituras caindo em `Thing:vinho001`: rode o 1.2b; se `count` for 2, apague com 5.1 (repita até 404), 5.3, e registre de novo com o 1.2 (que já traz `apikey TEF`) |
| 1.2 dá 409 | O dispositivo já existe (ou foi criado sozinho): 5.1 e repita |
| `timesSent` ausente em 1.7 | Nenhum evento chegou: o `uptime` precisa mudar (use `{{$timestamp}}` nas simulações) |
| Subscriptions duplicadas | Dados repetidos no histórico: apague as extras (deixe 4) |
| 1.3 a 1.6 dão erro de `attrsFormat` | O Orion 4.x não aceita `legacy`: as requisições da coleção já usam `httpCustom` |
| Comando em `PENDING` | ESP32 offline (o IoT Agent não reenvia); o supervisor reenvia após 30 s |
| Erro 400 ao salvar texto | Caracteres `< > " ' = ; ( )` não são aceitos pelo Orion |
| Alerta não dispara | App fechado? A média ainda não passou do limite (janela de ≈ 30 s)? Limites no `Dispositivo` |
