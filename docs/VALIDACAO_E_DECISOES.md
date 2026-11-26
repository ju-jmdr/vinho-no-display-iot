# Validação das ideias e decisões de projeto

## Ideias validadas
| Ideia | Viável? | Como foi feito |
|---|---|---|
| Mover configuração do display para o app | Sim | Comando `config` (5 valores separados por vírgula) via Orion → IoT Agent → MQTT; o ESP32 valida, grava na flash (`Preferences`) e confirma em `/cmdexe` |
| Mover consulta do log de anomalias para o app | Sim | O ESP32 publica o bitmask `alarm` (1 temp, 2 umid, 4 luz, 8 sensor); o STH-Comet guarda o histórico e a tela Mensagens IoT filtra "Só anomalias" |
| Um único botão alternando telas | Sim | GPIO 15, 4 telas (temperatura, umidade, luminosidade, sistema), cada uma com a grandeza e a hora |
| Animação de alerta na faixa amarela | Sim, com limite | A faixa amarela são as 16 primeiras linhas de módulos bicolores; a animação (faixa piscando + texto rolando + buzzer, 4 s) usa só essa região |
| Sem RTC | Sim | Hora por NTP (UTC-3) |
| Manter animação inicial | Sim | Portada do `animacao()` original (image00–image09, 500 ms por quadro, 1500 ms no último) para o OLED: garrafa serve a taça e aparece "Vinho no display" |

## Problemas do diagram.json enviado (corrigidos)
- DHT22 no GPIO 34, que é só entrada (o DHT precisa de pino bidirecional) → movido para GPIO 4.
- LDR no GPIO 23, que não tem ADC → movido para GPIO 32 (ADC1).
- Buzzer no mesmo pino do SCL do OLED → buzzer no GPIO 19.
- OLED sem SDA/SCL ligados → SDA 21, SCL 22.
- COM do LED RGB solto → ligado ao GND, com resistores de 220 Ω.
- Sensores alimentados em 5 V → 3V3.
- Três botões → um só (GPIO 15).

## Reaproveitado do projeto Smart Lamp / do outro projeto
- fiware-service `smart` e apikey `TEF` (o outro projeto usava um service diferente).
- Uma subscription do STH por atributo; faixas de armazenamento adequadas a vinho.

## Restrições do FIWARE tratadas
- Orion rejeita `< > " ' = ; ( )` em textos: o app valida antes de enviar; o `config` usa vírgula.
- STH-Comet devolve no máximo 100 registros por consulta: o app pagina com `hLimit/hOffset` quando há filtro de datas; sem datas usa `lastN`.
- Cada atributo tem `recvTime` próprio; o app junta as séries pelo horário mais próximo (tolerância 2,5 s).

## Limitações conhecidas
- O OLED monocromático do Wokwi não mostra a cor amarela.
- Comandos não são reenviados se o ESP32 estiver offline.
- Anomalias durante queda de Wi-Fi não são registradas (sem memória local).
- O app não tem login.
- O app foi testado contra um FIWARE simulado (mock) e o firmware apenas teve a sintaxe verificada; teste ponta a ponta na sua EC2.

## Requisitos do kickoff N2 (Sistemas Embarcados) e como foram atendidos

| Requisito do kickoff | Situação / decisão |
|---|---|
| DHT11 + LDR (0–100 %) | Firmware real usa **DHT11**; o Wokwi só simula DHT22, então `wokwi/sketch.ino` usa DHT22 (única diferença). Conversão do LDR documentada |
| ESP32 publica via MQTT no IoT Agent; device e entidade provisionados | Sim (`vinho001`, `urn:ngsi-ld:Vinho:001`), coleção do Postman completa |
| STH-Comet + MongoDB, consultado pela API | 4 subscriptions `httpCustom`; app consulta `:8666` |
| Dashboard: valores atuais e histórico das 3 grandezas | Painel atualiza a cada 5 s sem recarregar |
| **Controle: a aplicação analisa as médias e envia comando ao ESP32** | **Mudança:** antes o ESP32 comparava os valores sozinho. Agora o `SupervisorService` (app) calcula a média de N leituras, compara com os limites e envia `alerta|máscara` pelo Orion; o ESP32 só executa |
| Alertas: LED e buzzer distintos por grandeza; prioridade; normalidade desligada | Tabela no README. LED **RGB** (cores e ritmos distintos; o kickoff cita LED azul, mantido o RGB a pedido da equipe). **Normalidade: LED e buzzer desligados** (antes o LED ficava verde) |
| IP configurável no device e no front-end | Device: comando `broker IP` no Serial (grava na flash). Front-end: tela *Dispositivo* ou `appsettings.json` |
| Liberar só as portas usadas | Tabela de portas (22, 1026, 8666, 1883, 4041; 7896 opcional) |
| Documentação: README, diagrama, códigos comentados | `README.md`, `docs/arquitetura.png/svg`, código comentado |

Decisões do supervisor: janela de 6 leituras (≈ 30 s) configurável; envia só quando o alerta exigido difere do `alarm` informado pelo ESP32; ignora dados com mais de 60 s; reenvia após 30 s se o ESP32 não refletir. A prioridade entre grandezas é temperatura > umidade > luz.
