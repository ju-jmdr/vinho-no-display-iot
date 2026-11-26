// Vinho no Display IoT - ESP32 + FIWARE (IoT Agent UltraLight / MQTT)
// Sensores: DHT11 (temperatura/umidade) e LDR (luminosidade, escala 0-100 %)
// Saídas: OLED SSD1306 (faixa amarela = 16 primeiras linhas), buzzer e LED RGB
// Entrada: UM botão (toque = troca a tela; toque longo = silencia o buzzer)
//
// QUEM DECIDE O ALERTA: a aplicação (front-end ASP.NET). Ela calcula a MÉDIA das últimas
// leituras, compara com os limites e manda o comando "alerta" (máscara de bits) ao ESP32,
// passando pelo Orion -> IoT Agent -> MQTT. O ESP32 só executa: LED, buzzer e display.
// Sem alerta (máscara 0) LED e buzzer ficam DESLIGADOS.
//
// Tópicos (service group com apikey "TEF", dispositivo "vinho001"):
//   publica   /TEF/vinho001/attrs   -> a|mask|t|15.3|h|68.0|l|12
//                                      (u = uptime em segundos, sinal de vida)
//                                   -> tn|13.0|tx|18.0|hn|60.0|hx|75.0|lx|30.0
//   recebe    /TEF/vinho001/cmd     -> vinho001@alerta|5        (máscara: 1 temp, 2 umid, 4 luz)
//                                   -> vinho001@config|13.0,18.0,60.0,75.0,30.0
//                                   -> vinho001@testealerta|
//   confirma  /TEF/vinho001/cmdexe  -> vinho001@config|ok
#include <WiFi.h>
#include <PubSubClient.h>
#include <Preferences.h>
#include <Wire.h>
#include <Adafruit_GFX.h>
#include <Adafruit_SSD1306.h>
#include <DHT.h>
#include <time.h>

// Tipo declarado no topo: o Arduino gera prototipos de funcoes antes do codigo
struct PadraoAlerta { bool r, g, b; int periodo; int hz; };

// ---------- Configurações (edite aqui) ----------
const char* WIFI_SSID   = "Wokwi-GUEST";         // no ESP32 real: sua rede 2,4 GHz
const char* WIFI_PASS   = "";
const char* BROKER_MQTT = "COLE_AQUI_O_IP_DA_EC2";   // padrão; troque sem regravar: digite  broker 1.2.3.4  no Serial Monitor
const int   BROKER_PORT = 1883;
const char* ID_MQTT     = "fiware_vinho001";
const char* DEVICE_ID   = "vinho001";
const char* T_CMD       = "/TEF/vinho001/cmd";
const char* T_CMDEXE    = "/TEF/vinho001/cmdexe";
const char* T_ATTRS     = "/TEF/vinho001/attrs";

const long  FUSO_SEGUNDOS = -3 * 3600;           // UTC-3 (Brasília)

// ---------- Pinos ----------
const int PIN_DHT = 4;
const int PIN_LDR = 32;        // ADC1 (funciona junto com o Wi-Fi)
const int PIN_BUZZER = 19;
const int PIN_R = 25, PIN_G = 26, PIN_B = 27;   // LED RGB cátodo comum
const int PIN_BOTAO = 15;      // INPUT_PULLUP, outro lado no GND
const int PIN_SDA = 21, PIN_SCL = 22;
const bool LDR_INVERTIDO = false;               // true se o módulo LDR der leitura MENOR com mais luz
// Conversão do LDR: ADC de 12 bits (0..4095) -> luminosidade de 0 a 100 %  (0 = escuro, 100 = muito claro)

// Sensor DHT: ESTE arquivo é para o WOKWI, que só simula o DHT22.
// No ESP32 real (DHT11) use firmware/VinhoNoDisplay/VinhoNoDisplay.ino, onde TIPO_DHT é DHT11.
#define TIPO_DHT DHT22

// ---------- Display ----------
#define LARGURA 128
#define ALTURA 64
#define FAIXA_AMARELA 16        // em módulos bicolores, as 16 primeiras linhas são amarelas
Adafruit_SSD1306 oled(LARGURA, ALTURA, &Wire, -1);

// ---------- Objetos ----------
DHT dht(PIN_DHT, TIPO_DHT);
WiFiClient espClient;
PubSubClient MQTT(espClient);
Preferences prefs;

// ---------- Limites (gravados na flash) ----------
float tempMin = 12, tempMax = 18, umidMin = 60, umidMax = 75, luzMax = 30;

// ---------- Alarmes (bits) ----------
const int AL_TEMP = 1, AL_UMID = 2, AL_LUZ = 4, AL_SENSOR = 8;

// ---------- Estado ----------
float temperatura = NAN, umidade = NAN;
int luminosidade = 0;
int alarmeApp = 0;                               // máscara pedida pela aplicação (bits 1, 2, 4)
bool sensorFalhou = false;                       // falha de leitura do DHT (detectada aqui)
int alarme = 0, alarmeAnterior = -1;             // alarme = alarmeApp | AL_SENSOR; é o que vai no atributo "alarm"
bool mudo = false;                               // toque longo silencia o buzzer
unsigned long testeIni = 0, testeAte = 0;        // comando "testealerta": mostra os 3 padrões
String brokerAtual;                              // IP do FIWARE (salvo na flash; ver comando "broker")
int tela = 0;                                    // 0 temp, 1 umid, 2 luz, 3 sistema
const int NUM_TELAS = 4;
unsigned long tSensor = 0, tEnvio = 0, tMqtt = 0, tWifi = 0, tTela = 0, tPress = 0;
unsigned long alertaAte = 0;                     // animação de alerta ativa até este instante
String alertaTexto = "";
bool botaoAnterior = false;
bool telaForcada = true;                         // redesenha já (troca de tela, mudança de alarme)

// ================= LED / BUZZER =================
void cor(bool r, bool g, bool b) { digitalWrite(PIN_R, r); digitalWrite(PIN_G, g); digitalWrite(PIN_B, b); }

void buzzerInit() {
#if ESP_ARDUINO_VERSION_MAJOR >= 3
  ledcAttach(PIN_BUZZER, 2000, 8);
#else
  ledcSetup(4, 2000, 8); ledcAttachPin(PIN_BUZZER, 4);
#endif
}
void buzzerTom(int hz) {
#if ESP_ARDUINO_VERSION_MAJOR >= 3
  ledcWriteTone(PIN_BUZZER, hz);
#else
  ledcWriteTone(4, hz);
#endif
}

// ================= ANIMAÇÃO INICIAL =================
// Port da animação original (LCD 16x2, image00..image09): um vinho é servido
// numa taça e, no fim, aparece "Vinho no display". 10 quadros de 500 ms (o último
// fica 1500 ms), como no código original. A garrafa fica na faixa amarela.
const int GX = 56;             // centro da taça
const int GY = 20;             // topo da taça

void desenhaTaca(int nivel) {  // nivel 0..4
  const int altCopo = 18;
  for (int r = 0; r < altCopo; r++) {                       // contorno (trapézio)
    int meio = 13 - (r * 7) / altCopo;
    oled.drawPixel(GX - meio, GY + r, SSD1306_WHITE);
    oled.drawPixel(GX + meio, GY + r, SSD1306_WHITE);
  }
  oled.drawFastHLine(GX - 13, GY, 27, SSD1306_WHITE);
  oled.drawLine(GX - 6, GY + altCopo, GX + 6, GY + altCopo, SSD1306_WHITE);
  oled.drawFastVLine(GX, GY + altCopo, 10, SSD1306_WHITE);  // haste
  oled.drawFastHLine(GX - 7, GY + altCopo + 10, 15, SSD1306_WHITE);  // base
  int linhas = (nivel * altCopo) / 4;                       // vinho
  for (int r = altCopo - linhas; r < altCopo; r++) {
    int meio = 13 - (r * 7) / altCopo;
    oled.drawFastHLine(GX - meio, GY + r, meio * 2 + 1, SSD1306_WHITE);
  }
}

void desenhaGarrafaEmPe() {    // garrafa em pé, à direita, dentro da faixa amarela
  oled.drawRect(98, 7, 12, 9, SSD1306_WHITE);
  oled.drawRect(102, 0, 4, 7, SSD1306_WHITE);
}
void desenhaGarrafaInclinada() { // inclinada sobre a taça: boca em x=GX
  oled.drawRect(GX + 6, 2, 28, 11, SSD1306_WHITE);   // corpo
  oled.drawRect(GX - 4, 5, 10, 5, SSD1306_WHITE);    // gargalo
}
void desenhaFio(int tam) {     // fio de vinho caindo da boca até a taça
  int ate = GY + 2;
  int fim = (tam >= 3) ? ate : 10 + tam * 3;
  for (int y = 10; y <= fim; y++) oled.drawPixel(GX - 4, y, SSD1306_WHITE);
}

void quadroAnimacao(int q) {
  oled.clearDisplay();
  switch (q) {
    case 0: desenhaGarrafaEmPe(); desenhaTaca(0); break;
    case 1: desenhaGarrafaInclinada(); desenhaFio(1); desenhaTaca(0); break;
    case 2: desenhaGarrafaInclinada(); desenhaFio(2); desenhaTaca(0); break;
    case 3: desenhaGarrafaInclinada(); desenhaFio(3); desenhaTaca(1); break;
    case 4: desenhaGarrafaInclinada(); desenhaFio(3); desenhaTaca(2); break;
    case 5: desenhaGarrafaInclinada(); desenhaFio(3); desenhaTaca(3); break;
    case 6: desenhaGarrafaInclinada(); desenhaFio(3); desenhaTaca(4); break;
    case 7: desenhaGarrafaEmPe(); desenhaTaca(4); break;
    case 8: desenhaTaca(4); break;
    case 9:
      desenhaTaca(4);
      oled.setTextSize(1); oled.setTextColor(SSD1306_WHITE);
      oled.setCursor(16, 56); oled.print(F("Vinho no display"));
      break;
  }
  oled.display();
}

void animacaoInicial() {
  for (int q = 0; q < 10; q++) {
    quadroAnimacao(q);
    delay(q == 9 ? 1500 : 500);
  }
}

// ================= LIMITES (flash) =================
void carregaLimites() {
  prefs.begin("vinho", true);
  tempMin = prefs.getFloat("tn", tempMin); tempMax = prefs.getFloat("tx", tempMax);
  umidMin = prefs.getFloat("hn", umidMin); umidMax = prefs.getFloat("hx", umidMax);
  luzMax = prefs.getFloat("lx", luzMax);
  prefs.end();
}
void salvaLimites() {
  prefs.begin("vinho", false);
  prefs.putFloat("tn", tempMin); prefs.putFloat("tx", tempMax);
  prefs.putFloat("hn", umidMin); prefs.putFloat("hx", umidMax);
  prefs.putFloat("lx", luzMax);
  prefs.end();
}

// ================= MQTT =================
void publicaLimites() {
  char b[100];
  snprintf(b, sizeof(b), "tn|%.1f|tx|%.1f|hn|%.1f|hx|%.1f|lx|%.1f", tempMin, tempMax, umidMin, umidMax, luzMax);
  MQTT.publish(T_ATTRS, b);
}

void publicaLeitura() {
  char b[120];
  int n = snprintf(b, sizeof(b), "a|%d", alarme);
  if (!isnan(temperatura)) n += snprintf(b + n, sizeof(b) - n, "|t|%.1f", temperatura);
  if (!isnan(umidade))     n += snprintf(b + n, sizeof(b) - n, "|h|%.1f", umidade);
  n += snprintf(b + n, sizeof(b) - n, "|l|%d", luminosidade);
  // uptime muda a cada mensagem: faz o Orion notificar o histórico e serve de "sinal de vida"
  snprintf(b + n, sizeof(b) - n, "|u|%lu", millis() / 1000);
  MQTT.publish(T_ATTRS, b);
  Serial.println(String("Publicado: ") + b);
}

void ack(const String& cmd, const char* info) {
  MQTT.publish(T_CMDEXE, (String(DEVICE_ID) + "@" + cmd + "|" + info).c_str());
}

String textoAlarme(int a);                             // definida mais abaixo

void iniciaAlerta(const String& texto) {
  alertaTexto = texto;
  alertaAte = millis() + 4000;
}

// Aplica a máscara de alertas pedida pela aplicação (bits 1 temp, 2 umid, 4 luz).
void aplicaAlerta(int mascara) {
  mascara &= (AL_TEMP | AL_UMID | AL_LUZ);
  if (mascara != alarmeApp) {
    mudo = false;                                      // alerta novo volta a soar
    if (mascara != 0) iniciaAlerta(textoAlarme(mascara));   // animação na faixa amarela
  }
  alarmeApp = mascara;
  alarme = alarmeApp | (sensorFalhou ? AL_SENSOR : 0);
  alarmeAnterior = -1;                                 // publica já, para a aplicação ver o eco no atributo alarm
  telaForcada = true;
}

bool aplicaConfig(const String& par) {
  float v[5]; int k = 0, ini = 0;
  for (int i = 0; i <= (int)par.length() && k < 5; i++) {
    if (i == (int)par.length() || par[i] == ',') { v[k++] = par.substring(ini, i).toFloat(); ini = i + 1; }
  }
  if (k != 5) return false;
  if (v[0] < -10 || v[1] > 40 || v[0] >= v[1]) return false;
  if (v[2] < 0 || v[3] > 100 || v[2] >= v[3]) return false;
  if (v[4] < 0 || v[4] > 100) return false;
  tempMin = v[0]; tempMax = v[1]; umidMin = v[2]; umidMax = v[3]; luzMax = v[4];
  salvaLimites();
  return true;
}

void mqttCallback(char* topic, byte* payload, unsigned int length) {
  String msg;
  for (unsigned int i = 0; i < length; i++) msg += (char)payload[i];
  Serial.println("Comando: " + msg);
  int at = msg.indexOf('@'), bar = msg.indexOf('|');
  if (at < 0 || bar < 0 || msg.substring(0, at) != DEVICE_ID) return;
  String cmd = msg.substring(at + 1, bar), par = msg.substring(bar + 1);

  if (cmd == "alerta") {
    aplicaAlerta(par.toInt());
    ack(cmd, "ok");
  } else if (cmd == "config") {
    if (aplicaConfig(par)) { ack(cmd, "ok"); publicaLimites(); }
    else ack(cmd, "erro");
  } else if (cmd == "testealerta") {
    iniciaAlerta("TESTE DE ALERTA");
    testeIni = millis(); testeAte = testeIni + 6000;   // 2 s de cada padrão: temperatura, umidade, luz
    ack(cmd, "ok");
  }
}

void conectaMqtt() {
  if (WiFi.status() != WL_CONNECTED || MQTT.connected()) return;
  if (millis() - tMqtt < 3000) return;
  tMqtt = millis();
  if (MQTT.connect(ID_MQTT)) {
    Serial.println("MQTT conectado");
    MQTT.subscribe(T_CMD);
    publicaLimites();
    alarmeAnterior = -1;        // força o envio da leitura
  } else Serial.println("MQTT falhou, tentando de novo...");
}

void conectaWifi() {
  // Nao reinicia a tentativa antes de 30 s: reiniciar a cada poucos segundos
  // impede a conexao de terminar (principalmente no Wokwi, que e lento)
  if (WiFi.status() == WL_CONNECTED || millis() - tWifi < 30000) return;
  tWifi = millis();
  Serial.println("Conectando ao Wi-Fi...");
  WiFi.disconnect();
  WiFi.begin(WIFI_SSID, WIFI_PASS, 6);
}

// ================= SENSORES / ALARMES =================
void leSensores() {
  float t = dht.readTemperature(), h = dht.readHumidity();
  temperatura = t; umidade = h;                       // NAN se o sensor falhar
  int raw = analogRead(PIN_LDR);
  luminosidade = map(raw, 0, 4095, 0, 100);
  if (LDR_INVERTIDO) luminosidade = 100 - luminosidade;

  // Os limites NÃO são comparados aqui: isso é feito pela aplicação (médias). Só a falha do sensor é local.
  sensorFalhou = isnan(t) || isnan(h);
  alarme = alarmeApp | (sensorFalhou ? AL_SENSOR : 0);
}

String textoAlarme(int a) {
  String s = "";
  if (a & AL_TEMP) s += "TEMPERATURA ";
  if (a & AL_UMID) s += "UMIDADE ";
  if (a & AL_LUZ) s += "LUZ ";
  if (a & AL_SENSOR) s += "SENSOR ";
  s.trim();
  return s;
}

// ================= TELAS =================
String horaAtual() {
  struct tm ti;
  if (!getLocalTime(&ti, 0)) return "--:--";
  char b[8]; strftime(b, sizeof(b), "%H:%M", &ti);
  return String(b);
}

void faixa(const char* titulo) {          // texto na faixa amarela (y 0..15)
  oled.setTextSize(1); oled.setTextColor(SSD1306_WHITE);
  oled.setCursor(2, 4); oled.print(titulo);
  String h = horaAtual();
  oled.setCursor(LARGURA - 2 - 6 * h.length(), 4); oled.print(h);
}

void telaValor(const char* titulo, String valor, const char* unidade, String faixaIdeal, bool fora) {
  oled.setTextSize(3);
  int larg = 18 * valor.length();
  oled.setCursor((LARGURA - larg - 14) / 2, 24); oled.print(valor);
  oled.setTextSize(1); oled.print(unidade);
  oled.setCursor(2, 54); oled.print(faixaIdeal);
  oled.setCursor(LARGURA - 2 - 6 * (fora ? 6 : 2), 54); oled.print(fora ? "ALERTA" : "OK");
}

void desenhaTela() {
  oled.clearDisplay();
  oled.setTextColor(SSD1306_WHITE);

  bool alerta = millis() < alertaAte;
  if (alerta) {                                       // animação na faixa amarela
    bool inv = (millis() / 250) % 2;
    if (inv) oled.fillRect(0, 0, LARGURA, FAIXA_AMARELA, SSD1306_WHITE);
    oled.setTextColor(inv ? SSD1306_BLACK : SSD1306_WHITE);
    oled.setTextSize(1);
    String txt = "!! ALERTA: " + alertaTexto + " !!   ";
    int larg = 6 * txt.length();
    int x = LARGURA - (int)((millis() / 30) % (larg + LARGURA));
    oled.setCursor(x, 4); oled.print(txt);
    oled.setTextColor(SSD1306_WHITE);
  } else {
    switch (tela) {
      case 0: faixa("TEMPERATURA"); break;
      case 1: faixa("UMIDADE"); break;
      case 2: faixa("LUMINOSIDADE"); break;
      default: faixa("SISTEMA"); break;
    }
  }

  char ideal[24];
  switch (tela) {
    case 0:
      snprintf(ideal, sizeof(ideal), "ideal %.0f-%.0f", tempMin, tempMax);
      if (isnan(temperatura)) telaValor("", "--", "", ideal, true);
      else telaValor("", String(temperatura, 1), "C", ideal, alarme & AL_TEMP);
      break;
    case 1:
      snprintf(ideal, sizeof(ideal), "ideal %.0f-%.0f%%", umidMin, umidMax);
      if (isnan(umidade)) telaValor("", "--", "", ideal, true);
      else telaValor("", String(umidade, 1), "%", ideal, alarme & AL_UMID);
      break;
    case 2:
      snprintf(ideal, sizeof(ideal), "maximo %.0f%%", luzMax);
      telaValor("", String(luminosidade), "%", ideal, alarme & AL_LUZ);
      break;
    default:
      oled.setTextSize(1);
      oled.setCursor(2, 22); oled.print("Wi-Fi: "); oled.print(WiFi.status() == WL_CONNECTED ? "conectado" : "sem rede");
      oled.setCursor(2, 32); oled.print("MQTT : "); oled.print(MQTT.connected() ? "conectado" : "desconectado");
      oled.setCursor(2, 42); oled.print("Alarme: "); oled.print(alarme ? textoAlarme(alarme) : String("nenhum"));
      oled.setCursor(2, 54); oled.print(mudo ? "MUDO " : "IP "); oled.print(brokerAtual);
      break;
  }
  oled.display();
}

// ================= LED RGB + BUZZER =================
// Tabela de alertas (README): cada grandeza tem cor, ritmo de pisca e tom próprios.
//   Temperatura: LED vermelho, pisca rápido (400 ms),   buzzer 1000 Hz
//   Umidade    : LED azul,     pisca médio  (800 ms),   buzzer 1500 Hz
//   Luminosid. : LED amarelo,  pisca lento  (1600 ms),  buzzer 2200 Hz
//   Sensor     : LED magenta,  pisca        (800 ms),   sem buzzer
// Mais de uma anomalia: vale a prioridade  temperatura > umidade > luminosidade > sensor.
// Sem anomalia: LED e buzzer DESLIGADOS. O buzzer soa só na fase "LED aceso" de cada pisca.

PadraoAlerta padraoDe(int causa) {
  switch (causa) {
    case AL_TEMP:  return {true, false, false, 400, 1000};
    case AL_UMID:  return {false, false, true, 800, 1500};
    case AL_LUZ:   return {true, true, false, 1600, 2200};
    default:       return {true, false, true, 800, 0};       // falha de sensor
  }
}

int causaPrincipal(int mascara) {
  if (mascara & AL_TEMP) return AL_TEMP;
  if (mascara & AL_UMID) return AL_UMID;
  if (mascara & AL_LUZ) return AL_LUZ;
  if (mascara & AL_SENSOR) return AL_SENSOR;
  return 0;
}

void atualizaSaidas() {
  static int ultimoTom = -1;                           // só mexe no PWM quando o tom muda (poupa CPU/simulador)
  int causa = causaPrincipal(alarme);
  if (millis() < testeAte) {                           // comando testealerta: percorre os 3 padrões
    int fase = ((millis() - testeIni) / 2000) % 3;
    causa = fase == 0 ? AL_TEMP : (fase == 1 ? AL_UMID : AL_LUZ);
  }

  bool ligado = false; int tom = 0;
  if (causa != 0) {
    PadraoAlerta p = padraoDe(causa);
    ligado = (millis() % p.periodo) < (unsigned long)(p.periodo / 2);
    cor(ligado && p.r, ligado && p.g, ligado && p.b);
    if (ligado && !mudo) tom = p.hz;
  } else {
    cor(false, false, false);                          // normalidade: tudo desligado
  }
  if (tom != ultimoTom) { ultimoTom = tom; buzzerTom(tom); }
}

// ================= BOTÃO =================
// Toque curto: próxima tela. Toque longo (>= 1,2 s): silencia/reativa o buzzer.
void leBotao() {
  bool pressionado = digitalRead(PIN_BOTAO) == LOW;
  if (pressionado && !botaoAnterior) tPress = millis();
  if (!pressionado && botaoAnterior && millis() - tPress > 40) {   // soltou (com debounce)
    if (millis() - tPress >= 1200) {
      mudo = !mudo;
      Serial.println(mudo ? "Buzzer mudo" : "Buzzer ativo");
    } else {
      tela = (tela + 1) % NUM_TELAS;
      Serial.println("Tela " + String(tela));
    }
    telaForcada = true;
  }
  botaoAnterior = pressionado;
}

// ================= SERIAL: IP DO FIWARE =================
// Learner Lab: o IP público muda a cada sessão. Sem regravar o firmware, digite no Serial Monitor:
//   broker 54.1.2.3     (grava na flash e reconecta)      broker     (mostra o atual)
void trataSerial() {
  if (!Serial.available()) return;
  String l = Serial.readStringUntil('\n'); l.trim();
  if (!l.startsWith("broker")) return;
  String ip = l.substring(6); ip.trim();
  if (ip.length() >= 7) {
    brokerAtual = ip;
    prefs.begin("vinho", false); prefs.putString("broker", ip); prefs.end();
    MQTT.disconnect();
    MQTT.setServer(brokerAtual.c_str(), BROKER_PORT);
    tMqtt = 0;
    Serial.println("Broker salvo: " + brokerAtual);
  } else {
    Serial.println("Broker atual: " + brokerAtual);
  }
}

// ================= ARDUINO =================
void setup() {
  Serial.begin(115200);
  pinMode(PIN_R, OUTPUT); pinMode(PIN_G, OUTPUT); pinMode(PIN_B, OUTPUT);
  pinMode(PIN_BOTAO, INPUT_PULLUP);
  buzzerInit();
  cor(false, false, false);

  Wire.begin(PIN_SDA, PIN_SCL);
  if (!oled.begin(SSD1306_SWITCHCAPVCC, 0x3C)) Serial.println("OLED nao encontrado");
  dht.begin();
  carregaLimites();
  prefs.begin("vinho", true); brokerAtual = prefs.getString("broker", BROKER_MQTT); prefs.end();

  animacaoInicial();                                   // animação original do projeto

  WiFi.mode(WIFI_STA);
  WiFi.begin(WIFI_SSID, WIFI_PASS, 6);   // canal 6 = conexao rapida no Wokwi
  tWifi = millis();
  configTime(FUSO_SEGUNDOS, 0, "pool.ntp.org", "time.nist.gov");
  MQTT.setServer(brokerAtual.c_str(), BROKER_PORT);
  MQTT.setCallback(mqttCallback);
  leSensores();
}

void loop() {
  conectaWifi();
  conectaMqtt();
  MQTT.loop();
  leBotao();
  trataSerial();

  if (millis() - tSensor >= 2000) {                    // DHT: no mínimo 2 s entre leituras
    tSensor = millis();
    bool falhaAntes = sensorFalhou;
    leSensores();
    if (sensorFalhou && !falhaAntes) iniciaAlerta("FALHA NO SENSOR");
  }

  // envia a cada 5 s e imediatamente quando o alarme muda (o atributo alarm é o eco do que a aplicação pediu)
  if (MQTT.connected() && (alarme != alarmeAnterior || millis() - tEnvio >= 5000)) {
    tEnvio = millis();
    alarmeAnterior = alarme;
    publicaLeitura();
  }

  // redesenha a cada 1 s (150 ms durante o alerta, para a animação); o I2C do OLED é a parte mais pesada
  unsigned long periodoTela = (millis() < alertaAte) ? 150 : 1000;
  if (telaForcada || millis() - tTela >= periodoTela) { telaForcada = false; tTela = millis(); desenhaTela(); }
  atualizaSaidas();
  delay(5);                                            // cede CPU (e deixa o Wokwi mais rápido)
}
