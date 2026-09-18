# StressTest

Консольная стресс-тестовая программа для Windows на .NET 8. Нагружает систему по заданному профилю: CPU, RAM, диск. Управляется через текстовый файл `config.txt`. Поддерживает циклический режим (атака → отдых), репликацию процессов и маскировку под системные имена.

> ⚠️ **Внимание.** Программа нагружает ресурсы компьютера и маскируется под системные процессы. Антивирусы могут реагировать на её поведение как на riskware. Используйте **только на своей машине** и только в законных целях — для тестирования железа, проверки охлаждения, стресс-тестов.

---

## Содержание

- [Возможности](#возможности)
- [Сборка](#сборка)
- [Запуск](#запуск)
- [Где лежат файлы](#где-лежат-файлы)
- [Конфигурация](#конфигурация)
- [Параметры конфига](#параметры-конфига)
- [Примеры конфигов](#примеры-конфигов)
- [Как остановить](#как-остановить)
- [Как найти свои процессы](#как-найти-свои-процессы)
- [Частые вопросы](#частые-вопросы)
- [Ограничения](#ограничения)
- [Правовая оговорка](#правовая-оговорка)
- [Лицензия](#лицензия)

---

## Возможности

- **Нагрузка на CPU** — тяжёлые FPU-операции (`Sqrt`, `Sin`, `Cos`, `Pow`, `Exp`, `Log`) в несколько потоков.
- **Нагрузка на RAM** — выделение и удержание указанного объёма памяти, регулярное «освежение» страниц, чтобы физическая RAM была занята.
- **Нагрузка на диск** — периодическая запись и чтение временного файла.
- **Репликация** — каждый дублёр порождает детей по заданному коэффициенту и глубине.
- **Циклический режим (burst)** — периоды атаки чередуются с отдыхом: нарастание → атака → спад → отдых → снова.
- **Маскировка процессов** — копии запускаются под именами вида `{Prefix}Host`, `{Prefix}Runtime` и т.д.
- **Автозапуск** — опционально прописывается в `HKCU\...\Run`.
- **Горячее перечитывание конфига** — изменения подхватываются без перезапуска.
- **Очистка после себя** — папка с копиями удаляется при выходе.

---

## Сборка

**Требования:**

- Windows 10/11 x64
- .NET SDK 8.0 или новее ([скачать](https://dotnet.microsoft.com/download/dotnet))

**Сборка одной командой:**

```cmd
dotnet publish -c Release
```

Готовый exe появится в:

```
bin\Release\net8.0\win-x64\publish\
```

Скопируйте `StressTest.exe` в удобную папку — например, `C:\Tools\StressTest\`.

**Сборка в один файл (self-contained):**

```cmd
dotnet publish -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true
```

Получите один exe, без дополнительных DLL.

---

## Запуск

1. Положите `StressTest.exe` в удобную папку.
2. (Опционально) Создайте шаблон конфига:

   ```cmd
   StressTest.exe --write-config
   ```

   Рядом с exe появится `config.txt`.

3. Откройте `config.txt` блокнотом, поправьте параметры.
4. Запустите `StressTest.exe` двойным кликом.

При запуске откроется небольшое окно консоли с информацией о запуске, закроется само. Программа продолжит работу в фоне.

---

## Где лежат файлы

Всё складывается в **рабочую папку** — там, где лежит сам exe:

```
C:\Tools\StressTest\
├── StressTest.exe        ← оригинал, который вы запускаете
├── config.txt            ← рецепт (создать через --write-config)
├── workers\              ← копии процессов (маскированные)
│   ├── ZXHost.exe
│   ├── ZXRuntime.exe
│   └── ...
├── diskiotmp\            ← временные файлы для дисковой нагрузки
├── stress.log            ← создаётся ТОЛЬКО при проблемах
└── CRASH.txt             ← создаётся только если программа упала
```

Если папка exe защищена (например, `Program Files`), рабочая папка переключается на:

```
%LocalAppData%\StressTestCfg\
```

Точный путь всегда виден в окне уведомления при старте или через PowerShell.

---

## Конфигурация

Конфиг — текстовый файл `config.txt` рядом с exe.

**Формат:**

```ini
# Комментарий
Ключ=Значение
```

- Регистр ключей не важен: `Workers`, `WORKERS`, `workers` — одинаково.
- Пробелы вокруг `=` допустимы: `Workers = 4` — работает.
- Пустые строки игнорируются.
- Строки, начинающиеся с `#` или `;`, — комментарии.
- Ключи, которых нет в файле, берутся из значений по умолчанию в коде.
- Неизвестные ключи игнорируются с записью в `stress.log`.

**Горячее перечитывание.** Пока программа работает, `config.txt` проверяется каждые `ConfigReloadMs` мс (по умолчанию 2000). Изменения подхватываются автоматически. Уже запущенные дублёры не меняют свои параметры — только новые.

---

## Параметры конфига

Полный список ключей. Все ключи необязательны: если ключа нет в файле, берётся значение из кода.

### Сводная таблица

| Ключ | Тип | По умолчанию | Диапазон | Описание |
|---|---|---|---|---|
| `Prefix` | строка | `ZX` | 1–16 символов | Приставка имён процессов |
| `Workers` | int | `4` | ≥ 1 | Число живых дублёров |
| `SpawnDelayMs` | int | `20` | ≥ 0 | Пауза между запусками, мс |
| `WaveDelayMs` | int | `300` | ≥ 0 | Пауза между волнами, мс |
| `MbPerWorker` | int | `512` | ≥ 1 | МБ RAM на дублёра |
| `ThreadsPerWorker` | int | `2` | ≥ 1 | CPU-потоков на дублёра |
| `Seconds` | int | `60` | ≥ 1 | Время жизни дублёра, сек |
| `DiskBurnMb` | int | `16` | ≥ 0 | МБ для дисковой нагрузки (0 = выкл.) |
| `Waves` | int | `3` | ≥ 1 | Глубина репликации |
| `Fanout` | int | `2` | ≥ 1 | Детей на волну |
| `EnableWorkers` | bool | `true` | — | Создавать ли дублёров |
| `EnableReplication` | bool | `false` | — | Разрешить репликацию |
| `BurstMode` | bool | `false` | — | Включить циклы атака/отдых |
| `AttackSec` | int | `120` | ≥ 1 | Длительность атаки, сек |
| `RestSec` | int | `180` | ≥ 1 | Длительность отдыха, сек |
| `RampUpSec` | int | `15` | ≥ 0 | Плавное нарастание, сек |
| `RampDownSec` | int | `15` | ≥ 0 | Плавный спад, сек |
| `BurstStartDelaySec` | int | `0` | ≥ 0 | Задержка перед первым циклом, сек |
| `AutoStart` | bool | `false` | — | Запуск при входе в систему |
| `NotifyOnStart` | bool | `true` | — | Показывать окно при старте |
| `CleanWorkersOnExit` | bool | `true` | — | Удалять `workers\` при выходе |
| `NestedWorkers` | bool | `false` | — | Вложенные `workers\workers\...` |
| `ConfigReloadMs` | int | `2000` | ≥ 500 | Период перечитывания, мс |

### Формат значений

**bool** — принимает любое из:

- `true`, `yes`, `1`, `on`, `вкл` → истина
- `false`, `no`, `0`, `off`, `выкл` → ложь
- Всё остальное → ложь

**int** — целое число. Если значение ниже минимума — обрезается до минимума. Если не число — ключ игнорируется, берётся дефолт.

**string** — любая строка без пробелов, кавычек и символов `\ / : * ? " < > |`. Рекомендуется буквы, цифры, подчёркивание.

### Подробно по каждому ключу

#### `Prefix`

Приставка, из которой собираются имена процессов и копий.

```
Prefix=ZX
```

Получаются имена: `ZXHost`, `ZXRuntime`, `ZXBroker`, `ZXWorker`, `ZXService`, `ZXAgent`, `ZXNode`, `ZXCore`.

**Влияет на:**

- Имена копий в `workers\`.
- Имя родителя.
- Имя записи автозапуска: `{Prefix}AutoRun`.
- Имя временного bat-файла уведомления.

**Ограничения:** не пустой. Без пробелов, кавычек, слэшей. Латиница удобнее кириллицы в командной строке.

**Практика:** короткое латинское имя в 1–4 символа — `ZX`, `Qw`, `SS`, `Test`.

---

#### `Workers`

Сколько живых дублёров держать одновременно.

```
Workers=4
```

**Что значит:** родитель проверяет каждые 2 секунды, сколько его детей живо. Если меньше `Workers` — досыпает новых.

**Влияет на:** базовую нагрузку — `Workers × MbPerWorker` МБ RAM и `Workers × ThreadsPerWorker` CPU-потоков.

**Практика:** `1–2` легко, `4–6` средне, `8+` тяжело.

---

#### `SpawnDelayMs`

Пауза между запусками соседних дублёров, мс.

```
SpawnDelayMs=20
```

**Что значит:** между запусками N дублёров подряд родитель ждёт это время. Иначе получится «залп».

**Практика:** `0–10` резкий скачок, `20–100` плавно, `200+` постепенно.

---

#### `WaveDelayMs`

Пауза между волнами репликации, мс.

```
WaveDelayMs=300
```

**Что значит:** если `Waves > 1`, между поколениями родитель ждёт это время.

**Практика:** `100–500`.

---

#### `MbPerWorker`

Сколько МБ RAM выделяет и пиннит каждый дублёр.

```
MbPerWorker=512
```

**Что значит:** дублёр выделяет массив `MbPerWorker` МБ, закрепляет страницы (`pinned`) и периодически трогает их, чтобы ОС не выгрузила в swap.

**Влияет на:** потребление RAM. При `Workers=4, MbPerWorker=512` — 2 ГБ физически занятой памяти.

**Практика:** `64–128` легко, `256–512` ощутимо, `1024+` тяжело.

**Осторожно:** если суммарная память превысит физическую, система уйдёт в swap.

---

#### `ThreadsPerWorker`

Сколько CPU-потоков крутит каждый дублёр.

```
ThreadsPerWorker=2
```

**Что значит:** каждый поток — бесконечный цикл тяжёлых FPU-операций. Один поток загружает одно ядро на 100%.

**Влияет на:** общая нагрузка ≈ `Workers × ThreadsPerWorker` ядер.

**Практика:** `1` один поток, `2` по два, `4+` перегруз.

---

#### `Seconds`

Сколько секунд живёт один дублёр.

```
Seconds=60
```

**Что значит:** дублёр запускает таймер. По истечении — завершается сам. Родитель досыпает нового.

**Практика:** `10–30` короткие вспышки, `60–300` стабильно, `600+` долго.

---

#### `DiskBurnMb`

Размер временного файла для дисковой нагрузки, МБ.

```
DiskBurnMb=16
```

**Что значит:** дублёр пишет файл указанного размера в `diskiotmp\`, читает назад, пауза 50 мс, по кругу.

**Влияет на:** загрузку диска. На HDD — шум, на SSD — нагрев контроллера.

**Практика:** `0` выключить, `8–32` легко, `64–256` заметно.

---

#### `Waves`

Глубина репликации вглубь.

```
Waves=3
```

**Что значит:** сколько поколений порождает каждый дублёр. `1` — только родитель плодит, `2` — дети плодят внуков, `3` — внуки плодят правнуков.

**Влияет на:** итоговое число процессов.

Формула: `Workers × (1 + Fanout + Fanout² + ... + Fanout^(Waves−1))`.

**Практика:** `1` просто, `2–3` умеренно, `4+` сотни процессов.

---

#### `Fanout`

Сколько детей создаёт один дублёр за одну волну.

```
Fanout=2
```

**Влияет на:** рост процессов. При `Workers=4`:

| Waves | Fanout | Всего процессов |
|---|---|---|
| 1 | 1 | 4 |
| 2 | 2 | 12 |
| 3 | 2 | 28 |
| 3 | 3 | 52 |
| 4 | 3 | 160 |

**Практика:** `1` линейно, `2` умеренно, `3+` сотни.

---

#### `EnableWorkers`

Создавать ли дублёров вообще.

```
EnableWorkers=true
```

`true` — пул работает. `false` — родитель запускается, маскируется, но никого не плодит.

**Практика:** `false` для экстренной остановки через конфиг.

---

#### `EnableReplication`

Разрешить репликацию.

```
EnableReplication=false
```

`true` — дети плодят внуков по `Waves` и `Fanout`. `false` — родитель держит пул, дети не плодят.

**Практика:** обычно `false`.

---

#### `BurstMode`

Включить циклический режим.

```
BurstMode=true
```

Работа идёт не постоянно, а циклами: `[нарастание] → [атака] → [спад] → [отдых] → снова`.

**Практика:** `true` для вспышек, `false` для постоянного фона.

---

#### `AttackSec`

Длительность атаки в burst-режиме, секунды.

```
AttackSec=120
```

Сколько секунд длится фаза полной нагрузки.

---

#### `RestSec`

Длительность отдыха, секунды.

```
RestSec=180
```

Сколько секунд длится фаза тишины.

---

#### `RampUpSec`

Плавное нарастание, секунды.

```
RampUpSec=15
```

За сколько секунд пул растёт с 0 до `Workers`. `0` — мгновенно.

---

#### `RampDownSec`

Плавный спад, секунды.

```
RampDownSec=15
```

За сколько секунд пул сходит с `Workers` до 0. `0` — резко.

---

#### `BurstStartDelaySec`

Задержка перед первым циклом, секунды.

```
BurstStartDelaySec=0
```

Сколько ждать после запуска, прежде чем начать первый цикл. `0` — сразу.

**Практика:** `60–600` — замаскировать старт (полезно с автозапуском).

---

#### `AutoStart`

Запуск при входе в систему.

```
AutoStart=false
```

`true` — прописывает в `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` под именем `{Prefix}AutoRun`. `false` — удаляет запись.

**Практика:** `false`, если не нужно.

---

#### `NotifyOnStart`

Показывать окно при старте.

```
NotifyOnStart=true
```

`true` — открывает окно `cmd` с параметрами запуска на 5 секунд. `false` — не показывает.

**Практика:** `true` при отладке, `false` при обычной работе.

---

#### `CleanWorkersOnExit`

Удалять папку `workers\` при выходе.

```
CleanWorkersOnExit=true
```

`true` — при завершении родителя папка удаляется. `false` — остаётся, копии переиспользуются.

**Замечание:** при `taskkill /F` папка **не** удалится — процесс убит жёстко.

---

#### `NestedWorkers`

Создавать вложенные `workers\workers\...`.

```
NestedWorkers=false
```

`false` (**рекомендуется**) — одна папка `workers\` рядом с exe. `true` — копии создают свои `workers\` внутри. Растёт вложенность.

**Практика:** всегда `false`.

---

#### `ConfigReloadMs`

Как часто перечитывать конфиг, мс.

```
ConfigReloadMs=2000
```

Родитель проверяет `LastWriteTimeUtc` файла. Если изменился — перечитывает. Диапазон ≥ `500`.

**Практика:** `1000–3000`.

**Что перечитывается:** все параметры пула, burst, репликации, автозапуска, префикса. **Не** перечитываются: `NotifyOnStart`, `CleanWorkersOnExit`, `ConfigReloadMs` — применяются при следующем запуске.

---

## Примеры конфигов

### 1. Минимальный — «проверить, что работает»

```ini
# Самая лёгкая конфигурация. Один дублёр, мало памяти, коротко.
# Задача: убедиться, что программа запускается и находит конфиг.

Prefix=ZX
Workers=1
MbPerWorker=64
ThreadsPerWorker=1
Seconds=10
SpawnDelayMs=50
WaveDelayMs=500
DiskBurnMb=0
Waves=1
Fanout=1
EnableWorkers=true
EnableReplication=false
BurstMode=false
AutoStart=false
NotifyOnStart=true
CleanWorkersOnExit=true
NestedWorkers=false
ConfigReloadMs=2000
```

**Что даёт:** 1 родитель + 1 дублёр. ~64 МБ RAM. 1 поток CPU. 10 секунд жизни. Тишина.

---

### 2. Лёгкая фоновая нагрузка

```ini
# Заметно в диспетчере, но не мешает работать.

Prefix=ZX
Workers=2
MbPerWorker=128
ThreadsPerWorker=1
Seconds=15
SpawnDelayMs=20
WaveDelayMs=300
DiskBurnMb=8
Waves=1
Fanout=1
EnableWorkers=true
EnableReplication=false
BurstMode=false
AutoStart=false
NotifyOnStart=false
CleanWorkersOnExit=true
NestedWorkers=false
ConfigReloadMs=2000
```

**Что даёт:** 2 дублёра, 256 МБ RAM, 2 потока CPU, лёгкая дисковая нагрузка.

---

### 3. Средняя нагрузка с репликацией в 1 поколение

```ini
# 4 дублёра + их дети. Итого около 8-12 процессов.

Prefix=ZX
Workers=4
MbPerWorker=256
ThreadsPerWorker=2
Seconds=30
SpawnDelayMs=10
WaveDelayMs=200
DiskBurnMb=16
Waves=2
Fanout=2
EnableWorkers=true
EnableReplication=true
BurstMode=false
AutoStart=false
NotifyOnStart=true
CleanWorkersOnExit=true
NestedWorkers=false
ConfigReloadMs=2000
```

**Что даёт:** 4 × (1 + 2) = 12 процессов. ~3 ГБ RAM. ~8 потоков CPU. Диск.

---

### 4. Тяжёлая нагрузка — стресс

```ini
# Машина будет нагружена серьёзно. Хорошо для теста охлаждения.
# ВНИМАНИЕ: только для мощных машин или тестов "как оно ломается".

Prefix=ZX
Workers=6
MbPerWorker=512
ThreadsPerWorker=2
Seconds=60
SpawnDelayMs=10
WaveDelayMs=100
DiskBurnMb=32
Waves=3
Fanout=2
EnableWorkers=true
EnableReplication=true
BurstMode=false
AutoStart=false
NotifyOnStart=true
CleanWorkersOnExit=true
NestedWorkers=false
ConfigReloadMs=2000
```

**Что даёт:** 6 × (1 + 2 + 4) = 42 процесса. ~21 ГБ RAM. ~12 потоков CPU. Диск 32 МБ.

---

### 5. Циклический режим — вспышки с отдыхом

```ini
# 60 секунд атаки, потом 180 секунд отдыха, потом снова.
# Плавное нарастание и спад по 10 секунд.

Prefix=ZX
Workers=4
MbPerWorker=512
ThreadsPerWorker=2
Seconds=30
SpawnDelayMs=20
WaveDelayMs=300
DiskBurnMb=16
Waves=1
Fanout=1
EnableWorkers=true
EnableReplication=false
BurstMode=true
AttackSec=60
RestSec=180
RampUpSec=10
RampDownSec=10
BurstStartDelaySec=0
AutoStart=false
NotifyOnStart=false
CleanWorkersOnExit=true
NestedWorkers=false
ConfigReloadMs=2000
```

**Что даёт:** цикл = 10 + 60 + 10 + 180 = 260 сек. Атака 60 сек, потом 180 сек тишины.

---

### 6. Автозапуск с задержкой

```ini
# Программа стартует при входе в систему, ждёт 10 минут,
# потом начинает цикл 3 минуты атаки / 5 минут отдыха.

Prefix=ZX
Workers=4
MbPerWorker=512
ThreadsPerWorker=2
Seconds=60
DiskBurnMb=32
Waves=2
Fanout=2
EnableWorkers=true
EnableReplication=true
BurstMode=true
AttackSec=180
RestSec=300
RampUpSec=20
RampDownSec=20
BurstStartDelaySec=600
AutoStart=true
NotifyOnStart=false
CleanWorkersOnExit=false
NestedWorkers=false
ConfigReloadMs=2000
```

**Что даёт:** автозапуск, первые 10 минут ничего не происходит, потом цикл.

---

### 7. Диск-шторм — нагрузка на I/O

```ini
# CPU и RAM минимальны, диск работает на полную.
# Хорошо для теста HDD/SSD.

Prefix=ZX
Workers=4
MbPerWorker=64
ThreadsPerWorker=1
Seconds=60
SpawnDelayMs=10
WaveDelayMs=100
DiskBurnMb=128
Waves=1
Fanout=1
EnableWorkers=true
EnableReplication=false
BurstMode=false
AutoStart=false
NotifyOnStart=false
CleanWorkersOnExit=true
NestedWorkers=false
ConfigReloadMs=2000
```

**Что даёт:** 4 дублёра пишут и читают по 128 МБ. На HDD — жёстко. На SSD — заметно.

---

### 8. Взрыв числа процессов

```ini
# Задача — не нагрузка, а количество.
# 160 процессов, каждый ест по 64 МБ.

Prefix=ZX
Workers=4
MbPerWorker=64
ThreadsPerWorker=1
Seconds=30
SpawnDelayMs=1
WaveDelayMs=20
DiskBurnMb=0
Waves=4
Fanout=3
EnableWorkers=true
EnableReplication=true
BurstMode=false
AutoStart=false
NotifyOnStart=false
CleanWorkersOnExit=true
NestedWorkers=false
ConfigReloadMs=1000
```

**Что даёт:** 4 × (1 + 3 + 9 + 27) = 160 процессов. ~10 ГБ RAM.

---

### 9. Экстренная остановка

```ini
# Если нужно быстро выключить работающую программу:
# создай такой конфиг рядом с exe. Через 2 секунды родитель перестанет плодить.

Prefix=ZX
EnableWorkers=false
EnableReplication=false
BurstMode=false
AutoStart=false
```

**Что даёт:** родитель перестаёт создавать новых дублёров. Старые доживают `Seconds`.

---

### 10. Только маскировка, без нагрузки

```ini
# Родитель запускается, маскируется под {Prefix}Host,
# но ничего не делает. Полезно для теста самой маскировки.

Prefix=ZX
EnableWorkers=false
EnableReplication=false
BurstMode=false
AutoStart=false
NotifyOnStart=true
CleanWorkersOnExit=false
NestedWorkers=false
```

**Что даёт:** один процесс `ZXHost.exe`, висит в фоне, ничего не ест.

---

### Как выбирать шаблон

- **Начни с шаблона 1** — убедиться, что всё работает.
- **Шаблон 2** — фоновая нагрузка.
- **Шаблон 3** — «средне», с репликацией.
- **Шаблон 4** — реальный стресс. Осторожно.
- **Шаблон 5** — вспышки с отдыхом.
- **Шаблон 6** — автозапуск с задержкой.
- **Шаблон 7** — тест диска.
- **Шаблон 8** — тест планировщика ОС.
- **Шаблон 9** — экстренная остановка.
- **Шаблон 10** — тест маскировки.

---

## Как остановить

Программа — это **дерево процессов**: родитель и его дублёры. Чтобы остановить всё, надо убить **всё дерево**, начиная с родителя. Если убить только детей, родитель тут же досыпет новых.

### Способ 0. Сначала — убрать автозапуск

**Это важно сделать первым.** Если в конфиге было `AutoStart=true`, при перезагрузке программа вернётся.

**Через конфиг:**

```ini
AutoStart=false
```

Сохраните. Через 2 секунды родитель удалит запись из реестра.

**Вручную через реестр:**

`Win+R` → `regedit` → Enter. Перейдите в:

```
HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run
```

Найдите `{Prefix}AutoRun` (например, `ZXAutoRun`). Удалите.

**Через PowerShell:**

```powershell
Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' `
  -Name 'ZXAutoRun' -ErrorAction SilentlyContinue
```

Замените `ZX` на свой префикс.

---

### Способ 1. Через конфиг — мягко

Подходит, если родитель ещё жив и читает конфиг.

Откройте `config.txt`. Поставьте:

```ini
EnableWorkers=false
EnableReplication=false
BurstMode=false
AutoStart=false
```

Сохраните. Родитель перечитает через 2 секунды. Новые дублёры перестанут создаваться. Старые доживут `Seconds` и умрут.

**Минус:** родитель останется висеть.

---

### Способ 2. Через PowerShell — быстро и надёжно

```powershell
Get-CimInstance Win32_Process | Where-Object { $_.Name -like 'ZX*' } |
  ForEach-Object { taskkill /F /T /PID $_.ProcessId }
```

Замените `ZX` на свой префикс.

Если префикс совпадает с системным именем (`svchost` и т.п.) — фильтруйте по пути:

```powershell
Get-Process | Where-Object { $_.Path -like '*\workers\*' } | Stop-Process -Force
```

---

### Способ 3. Через `cmd` — по имени

Откройте `cmd`. Выполните по порядку:

```cmd
taskkill /F /T /IM ZXHost.exe
taskkill /F /T /IM ZXRuntime.exe
taskkill /F /T /IM ZXBroker.exe
taskkill /F /T /IM ZXWorker.exe
taskkill /F /T /IM ZXService.exe
taskkill /F /T /IM ZXAgent.exe
taskkill /F /T /IM ZXNode.exe
taskkill /F /T /IM ZXCore.exe
```

`/F` — принудительно. `/T` — убить дерево. `/IM` — по имени образа.

**Проверка:**

```cmd
tasklist | findstr ZX
```

Пусто — процессов нет.

---

### Способ 4. Через диспетчер задач

1. `Ctrl+Shift+Esc` → вкладка **«Подробности»**.
2. Правой по заголовку столбцов → включите **«Путь к образу»** и **«Командная строка»**.
3. Найдите процессы с путём в `workers\`.
4. **Сначала родителя** (без `--worker` в командной строке), потом остальных.

---

### Способ 5. Одной строкой — «убить всё»

PowerShell **от имени администратора**:

```powershell
# 1) Убить всё дерево
Get-CimInstance Win32_Process | Where-Object { $_.Name -like 'ZX*' } |
  ForEach-Object { taskkill /F /T /PID $_.ProcessId 2>&1 | Out-Null }

# 2) Убрать автозапуск
Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' `
  -Name 'ZXAutoRun' -ErrorAction SilentlyContinue

# 3) Проверить, что ничего не осталось
$left = Get-Process ZX* -ErrorAction SilentlyContinue
if ($left) { $left | Format-Table Id, ProcessName, Path }
else { Write-Host "Всё убито" }
```

---

### Способ 6. Крайний случай — перезагрузка

1. **Сначала** удалите автозапуск (см. Способ 0).
2. Перезагрузитесь.
3. **Не запускайте exe.**
4. В `config.txt` поставьте `EnableWorkers=false`, `AutoStart=false`.
5. Удалите рабочую папку и файлы (см. Способ 8).

---

### Способ 7. Безопасный режим — если ничего не помогает

1. **Сначала** удалите `{Prefix}AutoRun` в реестре.
2. Перезагрузитесь в **безопасном режиме**:
   - Зажмите `Shift` → «Перезагрузка» в меню Пуск.
   - Troubleshoot → Advanced options → Startup Settings → Restart.
   - Нажмите `4`.
3. В безопасном режиме автозапуск **не срабатывает**.
4. Удалите рабочую папку, exe и запись в реестре.
5. Перезагрузитесь в обычном режиме.

---

### Способ 8. Удалить файлы

```cmd
rmdir /S /Q "C:\путь\к\рабочей\папке\workers"
rmdir /S /Q "C:\путь\к\рабочей\папке\diskiotmp"
```

Или всю рабочую папку:

```cmd
rmdir /S /Q "C:\путь\к\рабочей\папке"
```

**Как узнать путь:** окно уведомления при запуске (`Рабочая папка: ...`), либо через PowerShell:

```powershell
Get-CimInstance Win32_Process | Where-Object { $_.Name -like 'ZX*' } |
  Select-Object ProcessId, Name, ExecutablePath | Format-Table -AutoSize
```

**Типичные места:**

- Рядом с exe: `C:\...\workers\`.
- Fallback: `%LocalAppData%\StressTestCfg\workers\`.

---

### Частые ошибки при остановке

**Убить детей, оставить родителя.** Родитель тут же досыпет новых. **Сначала родителя.**

**Забыть автозапуск.** Перезагрузитесь — всё вернётся. **Сначала уберите запись в реестре.**

**Убить по имени системные процессы.** Если маскировались под `svchost` — `taskkill /IM svchost.exe` убьёт **настоящие** процессы Windows. **Фильтруйте по пути.**

**`taskkill /IM` без `/T`.** Дети могут пережить родителя. **Всегда `/T`.**

**Без прав администратора.** Если exe был в защищённой папке — запустите `cmd` от админа.

**Несколько независимых родителей.** Запускали exe несколько раз — у каждого свой родитель. Убейте **всех** `ZXHost.exe` сразу:

```powershell
Get-Process ZXHost -ErrorAction SilentlyContinue | Stop-Process -Force
```

---

### Проверочный чек-лист

- [ ] `Get-Process ZX*` возвращает пустоту.
- [ ] В диспетчере нет процессов `ZXHost`, `ZXRuntime`, `ZXBroker`.
- [ ] В реестре `HKCU\...\Run` нет записи `ZXAutoRun`.
- [ ] После перезагрузки процессы **не появляются**.
- [ ] Папка `workers\` пуста или удалена.
- [ ] Папка `diskiotmp\` удалена.
- [ ] `StressTest.exe` удалён с диска.
- [ ] `%LocalAppData%\StressTestCfg\` пуста, если был fallback.

**Проверка одной командой:**

```powershell
$procs = Get-Process ZX* -ErrorAction SilentlyContinue
$run   = Get-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -ErrorAction SilentlyContinue |
         Select-Object -ExpandProperty ZXAutoRun -ErrorAction SilentlyContinue

if (-not $procs -and -not $run) { Write-Host "Остановлено" }
else {
    if ($procs) { Write-Host "Остались процессы:"; $procs | Format-Table Id, ProcessName, Path }
    if ($run)   { Write-Host "Автозапуск прописан: $run" }
}
```

---

### Самая короткая инструкция

1. Откройте `config.txt`. Поставьте `AutoStart=false`, `EnableWorkers=false`. Сохраните.
2. Подождите 5 секунд.
3. PowerShell **от имени администратора**:

   ```powershell
   Get-CimInstance Win32_Process | Where-Object { $_.Name -like 'ZX*' } |
     ForEach-Object { taskkill /F /T /PID $_.ProcessId }
   ```

4. Проверьте: `Get-Process ZX*`. Должно быть пусто.
5. Удалите рабочую папку, `workers\`, `diskiotmp\`.
6. Проверьте реестр на `ZXAutoRun`.

---

## Как найти свои процессы

### По префиксу

```powershell
Get-Process ZX* | Select-Object Id, ProcessName, Path
```

### По пути

```powershell
Get-CimInstance Win32_Process |
  Where-Object { $_.ExecutablePath -like '*\workers\*' } |
  Select-Object ProcessId, Name, ExecutablePath | Format-Table -AutoSize
```

### По командной строке

```powershell
Get-CimInstance Win32_Process |
  Where-Object { $_.CommandLine -like '*--prefix ZX*' } |
  Select-Object ProcessId, Name, CommandLine | Format-Table -AutoSize
```

### В диспетчере задач

1. `Ctrl+Shift+Esc` → вкладка **«Подробности»**.
2. Правой по заголовку → **«Выбрать столбцы»** → включите **«Имя»**, **«Путь к образу»**, **«Командная строка»**.
3. Ищите процессы с путём в `workers\`.

**Почему во вкладке «Процессы» они без имени:** Windows группирует дерево, имя показывается только у корня. Смотрите вкладку «Подробности» или PowerShell.

---

## Частые вопросы

**Почему процессов без имени в диспетчере?**
Во вкладке «Процессы» Windows группирует дерево: имя у корня, дети без подписи. Смотрите «Подробности».

**Конфиг не читается.**
Проверьте, что `config.txt` лежит в одной папке с exe (или в `%LocalAppData%\StressTestCfg\`). Точный путь — в окне уведомления при старте.

**`stress.log` не создаётся.**
Так задумано. Лог пишется только при проблемах.

**`workers\` исчезла.**
Сработала `CleanWorkersOnExit=true`. Поставьте `false`, чтобы сохранять.

**`workers\workers\workers\...`**
Поставьте `NestedWorkers=false`. Удалите лишние папки вручную.

**Антивирус удаляет exe.**
Добавьте папку в исключения Defender. Программа самокопируется и маскируется — реакция ожидаема.

**После перезагрузки процессы вернулись.**
Был `AutoStart=true`. Уберите запись из реестра.

**Программа не запускается, окно мелькает.**
Запустите из `cmd` — увидите ошибку. Или проверьте `CRASH.txt`.

---

## Ограничения

- Только Windows (используются `kernel32.dll`, `user32.dll`, реестр).
- Требует .NET 8 runtime, если собирали без `--self-contained`.
- Не работает на Linux/macOS.
- Маскировка косметическая (имя + описание). Путь остаётся в `workers\`, издатель — «Неизвестно».
- При `taskkill /F` очистка папки не срабатывает.

---

## Правовая оговорка

Программа **нагружает ресурсы компьютера** и **маскируется под системные процессы**. Такое поведение **совпадает** с поведением вредоносного ПО, и антивирусы могут реагировать соответственно.

**Разрешено:**

- Использовать на **своей** машине.
- Использовать для тестирования охлаждения, стабильности, стресс-тестов.
- Модифицировать под собственные задачи.

**Запрещено:**

- Запускать на **чужих** компьютерах без разрешения владельца.
- Использовать для атак, DDoS, дестабилизации чужих систем.
- Передавать третьим лицам как инструмент вредительства.

**Автор не несёт ответственности** за любой ущерб, вызванный использованием программы. Используйте на свой риск.

---

## Лицензия

MIT. Делайте что хотите, только не во вред другим.

---

## Вклад

Issues и pull requests приветствуются. Если добавляете новые нагрузки — описывайте, что они делают и как настраиваются через конфиг.
