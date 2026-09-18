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
- [Заполнение памяти картинками](#заполнение-памяти-картинками)
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
- **Нагрузка на RAM** — выделение и удержание указанного объёма памяти, регулярное «освежение» страниц, чтобы физическая RAM была занята. Поддерживает заполнение картинками (вшитыми или из папки).
- **Нагрузка на диск** — периодическая запись и чтение временного файла. **По умолчанию выключена.**
- **Репликация** — каждый дублёр порождает детей по заданному коэффициенту и глубине.
- **Циклический режим (burst)** — периоды атаки чередуются с отдыхом: нарастание → атака → спад → отдых → снова.
- **Маскировка процессов** — копии запускаются под именами вида `{Prefix}Host`, `{Prefix}Runtime` и т.д.
- **Автозапуск** — опционально прописывается в `HKCU\...\Run`.
- **Горячее перечитывание конфига** — изменения подхватываются без перезапуска.
- **Ограничение времени работы** — опционально, через `MaxRunSeconds`.
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

Скопируйте exe в удобную папку — например, `C:\Tools\StressTest\`.

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

1. Положите exe в удобную папку.
2. (Опционально) Создайте шаблон конфига:

   ```cmd
   Svchost.exe --write-config
   ```

   Рядом с exe появится `config.txt`.

3. (Опционально) Положите картинки:
   - В папку `images\` рядом с exe — для режима `folder` или `maybetrue`.
   - Или в папку `pictures\` **в проекте** — они вшиваются в exe при сборке.

4. Откройте `config.txt` блокнотом, поправьте параметры.
5. Запустите exe двойным кликом.

При запуске откроется небольшое окно консоли с информацией о запуске, закроется само. Программа продолжит работу в фоне.

---

## Где лежат файлы

Всё складывается в **рабочую папку** — там, где лежит сам exe:

```
C:\Tools\StressTest\
├── Svchost.exe            ← оригинал, который вы запускаете
├── config.txt             ← рецепт (создать через --write-config)
├── images\                ← внешние картинки (для режима folder / maybetrue)
│   ├── img1.jpeg
│   └── ...
├── workers\               ← копии процессов (маскированные)
│   ├── ZHost.exe
│   ├── ZRuntime.exe
│   └── ...
├── diskiotmp\             ← временные файлы для дисковой нагрузки
│                            (создаётся только если DiskBurnMb > 0)
├── stress.log             ← создаётся ТОЛЬКО при проблемах
└── CRASH.txt              ← создаётся только если программа упала
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

**Важно:** параметры `PicturesSource`, `ImagesDir`, `MaxRunSeconds`, `NotifyOnStart`, `CleanWorkersOnExit`, `NestedWorkers` **не перечитываются** на ходу. Их изменения требуют перезапуска родителя.

---

## Параметры конфига

Полный список ключей. Все ключи необязательны: если ключа нет в файле, берётся значение из кода.

### Сводная таблица

| Ключ | Тип | По умолчанию | Диапазон | Описание |
|---|---|---|---|---|
| `Prefix` | строка | `Z` | 1–16 символов | Приставка имён процессов |
| `Workers` | int | `4` | ≥ 1 | Число живых дублёров |
| `SpawnDelayMs` | int | `20` | ≥ 0 | Пауза между запусками, мс |
| `WaveDelayMs` | int | `300` | ≥ 0 | Пауза между волнами, мс |
| `MbPerWorker` | int | `512` | ≥ 1 | МБ RAM на дублёра |
| `ThreadsPerWorker` | int | `2` | ≥ 1 | CPU-потоков на дублёра |
| `Seconds` | int | `60` | ≥ 1 | Время жизни дублёра, сек |
| `DiskBurnMb` | int | `0` | ≥ 0 | МБ для дисковой нагрузки (0 = выкл., **по умолчанию**) |
| `Waves` | int | `3` | ≥ 1 | Глубина репликации |
| `Fanout` | int | `2` | ≥ 1 | Детей на волну |
| `EnableWorkers` | bool | `true` | — | Создавать ли дублёров |
| `EnableReplication` | bool | `false` | — | Разрешить репликацию |
| `StopParentWhenDisabled` | bool | `false` | — | Завершать родителя при `EnableWorkers=false` |
| `BurstMode` | bool | `false` | — | Включить циклы атака/отдых |
| `AttackSec` | int | `120` | ≥ 1 | Длительность атаки, сек |
| `RestSec` | int | `180` | ≥ 1 | Длительность отдыха, сек |
| `RampUpSec` | int | `15` | ≥ 0 | Плавное нарастание, сек |
| `RampDownSec` | int | `15` | ≥ 0 | Плавный спад, сек |
| `BurstStartDelaySec` | int | `0` | ≥ 0 | Задержка перед первым циклом, сек |
| `AutoStart` | bool | `false` | — | Запуск при входе в систему |
| `NotifyOnStart` | bool | `true` | — | Показывать окно при старте |
| `NotifyTimeoutSec` | int | `1` | ≥ 0 | Сколько секунд висит окно |
| `CleanWorkersOnExit` | bool | `true` | — | Удалять `workers\` при выходе |
| `NestedWorkers` | bool | `false` | — | Вложенные `workers\workers\...` |
| `PicturesSource` | строка | `embedded` | — | Источник картинок (см. ниже) |
| `ImagesDir` | строка | `images` | — | Папка с внешними картинками |
| `MaxRunSeconds` | int | `0` | ≥ 0 | Ограничение времени работы, сек (0 = без ограничения) |
| `ConfigReloadMs` | int | `2000` | ≥ 500 | Период перечитывания, мс |

### Формат значений

**bool** — принимает любое из:

- `true`, `yes`, `1`, `on`, `вкл` → истина
- `false`, `no`, `0`, `off`, `выкл` → ложь
- Всё остальное → ложь

**int** — целое число. Если значение ниже минимума — обрезается до минимума. Если не число — ключ игнорируется, берётся дефолт.

**string** — любая строка без пробелов, кавычек и символов `\ / : * ? " < > |`.

### Подробно по ключам

#### `Prefix`

Приставка, из которой собираются имена процессов и копий.

```
Prefix=Z
```

Получаются имена: `ZHost`, `ZRuntime`, `ZBroker`, `ZWorker`, `ZService`, `ZAgent`, `ZNode`, `ZCore`.

**Влияет на:** имена копий в `workers\`, имя родителя, имя записи автозапуска (`{Prefix}AutoRun`), имя временного bat-файла уведомления.

**Практика:** короткое латинское имя в 1–4 символа.

---

#### `Workers`

Сколько живых дублёров держать одновременно.

**Влияет на:** базовую нагрузку — `Workers × MbPerWorker` МБ RAM и `Workers × ThreadsPerWorker` CPU-потоков.

**Практика:** `1–2` легко, `4–6` средне, `8+` тяжело.

---

#### `SpawnDelayMs`

Пауза между запусками соседних дублёров, мс.

**Практика:** `0–10` резкий скачок, `20–100` плавно, `200+` постепенно.

---

#### `WaveDelayMs`

Пауза между волнами репликации, мс.

---

#### `MbPerWorker`

Сколько МБ RAM выделяет и пиннит каждый дублёр. Заполняется либо пустым массивом, либо картинками (см. `PicturesSource`).

**Практика:** `64–128` легко, `256–512` ощутимо, `1024+` тяжело.

---

#### `ThreadsPerWorker`

Сколько CPU-потоков крутит каждый дублёр.

**Влияет на:** общая нагрузка ≈ `Workers × ThreadsPerWorker` ядер.

---

#### `Seconds`

Сколько секунд живёт один дублёр. По истечении — завершается сам, родитель досыпает нового.

**Практика:** `10–30` короткие вспышки, `60–300` стабильно, `600+` долго.

---

#### `DiskBurnMb`

Размер временного файла для дисковой нагрузки, МБ.

**По умолчанию `0` — диск НЕ используется.** Чтобы включить, явно укажи `DiskBurnMb` > 0.

**Практика:** `0` выключить (по умолчанию), `8–32` легко, `64–256` заметно.

**Осторожно:** `DiskBurnMb=0` **не создаёт** папку `diskiotmp\`. Она появляется только при `DiskBurnMb > 0`.

---

#### `Waves`

Глубина репликации вглубь. Формула числа процессов:

`Workers × (1 + Fanout + Fanout² + ... + Fanout^(Waves−1))`.

**Практика:** `1` просто, `2–3` умеренно, `4+` сотни процессов.

---

#### `Fanout`

Сколько детей создаёт один дублёр за волну.

---

#### `EnableWorkers`

Создавать ли дублёров вообще. `false` — родитель запускается, маскируется, но никого не плодит.

**Практика:** `false` для экстренной остановки (в связке с `StopParentWhenDisabled`).

---

#### `EnableReplication`

Разрешить репликацию. `true` — дети плодят внуков по `Waves` и `Fanout`.

---

#### `StopParentWhenDisabled`

Если `true` и `EnableWorkers=false` — родитель **завершается сам**, а не уходит в паузу. Сработает `CleanWorkersOnExit`.

Если `false` (по умолчанию) — родитель **уходит в паузу**, продолжает следить за конфигом. Если потом вернуть `EnableWorkers=true` — возобновит работу.

---

#### `BurstMode`

Включить циклический режим: `[нарастание] → [атака] → [спад] → [отдых] → снова`.

---

#### `AttackSec`, `RestSec`, `RampUpSec`, `RampDownSec`, `BurstStartDelaySec`

Параметры burst-режима. Полный цикл = `RampUpSec + AttackSec + RampDownSec + RestSec` секунд.

---

#### `AutoStart`

Запуск при входе в систему. `true` — прописывает `{Prefix}AutoRun` в `HKCU\...\Run`. `false` — удаляет запись.

---

#### `NotifyOnStart`

Показывать окно `cmd` с параметрами запуска при старте родителя.

---

#### `NotifyTimeoutSec`

Сколько секунд окно уведомления висит на экране. `0` — закрывается мгновенно.

**По умолчанию `1`.**

---

#### `CleanWorkersOnExit`

Удалять папку `workers\` при выходе родителя. `true` — удалять, `false` — оставить.

**Замечание:** если дублёры ещё живы — папка занята. Удаление произойдёт, когда они умрут. При `StopParentWhenDisabled=true` дублёры убиваются перед выходом, и папка удаляется сразу.

---

#### `NestedWorkers`

`false` (рекомендуется) — одна папка `workers\`. `true` — вложенные `workers\workers\...`.

---

#### `PicturesSource`

Источник картинок для заполнения RAM. Три режима + отключение:

| Значение | Что делает |
|---|---|
| `true` | Только **встроенные** картинки (из exe). |
| `false` | Только картинки из папки `images\`. Если папки нет — **fallback** на встроенные. |
| `maybetrue` | И встроенные, и из папки. Если папки нет — только встроенные. |
| `none` | Картинки **не использовать**, буфер заполняется пустым массивом. |

Принимает также: `yes/no/1/0/on/off/вкл/выкл`, `both/maybe`, `нет`.

**По умолчанию `embedded`** (то же, что `true`).

---

#### `ImagesDir`

Имя папки с внешними картинками. По умолчанию `images`. Ищется рядом с exe (в рабочей папке).

---

#### `MaxRunSeconds`

Ограничение времени работы родителя, секунды.

- `0` (**по умолчанию**) — без ограничения.
- `>0` — через это время родитель **сам завершается**, убивает живых дублёров, удаляет `workers\` (если `CleanWorkersOnExit=true`).

**Примеры:** `30` = 30 сек, `300` = 5 мин, `3600` = 1 час, `86400` = 24 часа.

**Важно:** `MaxRunSeconds` **не перечитывается** на ходу. Изменение требует перезапуска.

---

#### `ConfigReloadMs`

Как часто перечитывать конфиг, мс. Родитель проверяет `LastWriteTimeUtc` файла.

**Что перечитывается на ходу:** `Workers`, `MbPerWorker`, `ThreadsPerWorker`, `Seconds`, `SpawnDelayMs`, `WaveDelayMs`, `Waves`, `Fanout`, `EnableWorkers`, `EnableReplication`, `StopParentWhenDisabled`, `AutoStart`, все burst-параметры, `Prefix`.

**Что НЕ перечитывается:** `PicturesSource`, `ImagesDir`, `MaxRunSeconds`, `NotifyOnStart`, `NotifyTimeoutSec`, `CleanWorkersOnExit`, `NestedWorkers`, `ConfigReloadMs`.

---

## Заполнение памяти картинками

Программа может заполнять буфер памяти **картинками**, а не пустым массивом. Это делает память «живой» — Windows не может её эффективно сжать.

### Источники картинок

**1. Вшитые в exe (`pictures\`).**

Файлы из папки `pictures\` **в проекте** вшиваются в exe при сборке через `<EmbeddedResource>` в `.csproj`. Читаются через `Assembly.GetManifestResourceStream`.

**2. Внешняя папка (`images\`).**

Папка `images\` **рядом с exe** содержит обычные файлы. Читаются через `File.ReadAllBytes`.

### Логика выбора

| `PicturesSource` | Папка `images\` есть? | Встроенные есть? | Что используется |
|---|---|---|---|
| `true` | — | да | встроенные |
| `true` | — | нет | пустой массив + `LogProblem` |
| `false` | да | — | из папки |
| `false` | **нет** | да | **встроенные (fallback)** |
| `false` | **нет** | нет | пустой массив + `LogProblem` |
| `maybetrue` | да | да | **встроенные + из папки** |
| `maybetrue` | **нет** | да | встроенные |
| `maybetrue` | да | нет | из папки |
| `maybetrue` | **нет** | нет | пустой массив + `LogProblem` |
| `none` | — | — | пустой массив |

**Ключевое:** при `false` и `maybetrue` — если папки `images\` нет, программа **не падает**, а использует встроенные картинки.

### Поддерживаемые форматы

`.jpg`, `.jpeg`, `.png`, `.bmp`, `.gif`.

### Как добавить свои картинки

**Встроенные:**

1. Создай папку `pictures\` рядом с `Program.cs` и `.csproj`.
2. Положи туда картинки `img1.jpeg`, `img2.jpeg` и т.д.
3. Убедись, что в `.csproj` есть:
   ```xml
   <ItemGroup>
     <EmbeddedResource Include="pictures\*.jpeg" />
     <EmbeddedResource Include="pictures\*.jpg" />
     <EmbeddedResource Include="pictures\*.png" />
     <EmbeddedResource Include="pictures\*.bmp" />
     <EmbeddedResource Include="pictures\*.gif" />
   </ItemGroup>
   ```
4. Пересобери проект.

**Внешние:**

1. Создай папку `images\` **рядом с exe** (в рабочей папке).
2. Положи туда картинки.
3. В конфиге: `PicturesSource=false` (только папка) или `maybetrue` (папка + встроенные).

**Совет:** в `.csproj` можно указать автокопирование `images\` в папку сборки:

```xml
<ItemGroup>
  <None Update="images\**\*">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

Тогда папка `images\` из проекта будет копироваться в `bin\Debug\...\images\` и `publish\images\`.

### Что остаётся после остановки

- **Встроенные картинки** — часть exe. Отдельно не лежат.
- **Картинки в `images\`** — твои файлы. Программа их **не удаляет**.
- **Буфер в RAM** — освобождается при выходе дублёра.

Ничего лишнего на диске не остаётся.

---

## Примеры конфигов

### 1. Проверка — «оно вообще работает?»

```ini
Prefix=Z
Workers=1
SpawnDelayMs=50
WaveDelayMs=500
MbPerWorker=64
ThreadsPerWorker=1
Seconds=10
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
PicturesSource=none
MaxRunSeconds=0
ConfigReloadMs=2000
```

### 2. Лёгкая фоновая нагрузка

```ini
Prefix=Z
Workers=2
MbPerWorker=128
ThreadsPerWorker=1
Seconds=15
DiskBurnMb=0
Waves=1
Fanout=1
EnableWorkers=true
EnableReplication=false
BurstMode=false
AutoStart=false
NotifyOnStart=false
CleanWorkersOnExit=true
NestedWorkers=false
PicturesSource=embedded
MaxRunSeconds=0
ConfigReloadMs=2000
```

### 3. Средняя нагрузка с репликацией

```ini
Prefix=Z
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
PicturesSource=embedded
MaxRunSeconds=0
ConfigReloadMs=2000
```

**Что даёт:** 4 × (1 + 2) = 12 процессов. ~3 ГБ RAM. ~8 потоков CPU. Диск 16 МБ.

### 4. Тяжёлая нагрузка — стресс

```ini
Prefix=Z
Workers=6
MbPerWorker=512
ThreadsPerWorker=2
Seconds=60
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
PicturesSource=embedded
MaxRunSeconds=0
ConfigReloadMs=2000
```

**Что даёт:** 6 × (1 + 2 + 4) = 42 процесса. ~21 ГБ RAM. ~12 потоков CPU.

### 5. Вспышки — атака/отдых

```ini
Prefix=Z
Workers=4
MbPerWorker=512
ThreadsPerWorker=2
Seconds=30
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
PicturesSource=embedded
MaxRunSeconds=0
ConfigReloadMs=2000
```

**Цикл:** 10 + 60 + 10 + 180 = 260 сек.

### 6. Автозапуск с задержкой

```ini
Prefix=Z
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
PicturesSource=embedded
MaxRunSeconds=0
ConfigReloadMs=2000
```

### 7. Диск-шторм

**Важно:** теперь для дисковой нагрузки нужно **явно** указать `DiskBurnMb > 0`.

```ini
Prefix=Z
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
PicturesSource=none
MaxRunSeconds=0
ConfigReloadMs=2000
```

### 8. Взрыв числа процессов

```ini
Prefix=Z
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
PicturesSource=none
MaxRunSeconds=0
ConfigReloadMs=1000
```

**Что даёт:** 4 × (1 + 3 + 9 + 27) = 160 процессов.

### 9. Экстренная остановка (пауза)

```ini
EnableWorkers=false
EnableReplication=false
StopParentWhenDisabled=false
BurstMode=false
AutoStart=false
```

Родитель переходит в **паузу**. Нагрузка спадает. Позже можно вернуть `EnableWorkers=true` — родитель возобновит работу.

### 10. Полная остановка (родитель выходит)

```ini
EnableWorkers=false
EnableReplication=false
StopParentWhenDisabled=true
BurstMode=false
AutoStart=false
CleanWorkersOnExit=true
```

Родитель **сам завершается** через 2 секунды. Убирает автозапуск. Удаляет `workers\`.

### 11. Только маскировка, без нагрузки

```ini
Prefix=Z
EnableWorkers=false
EnableReplication=false
BurstMode=false
AutoStart=false
NotifyOnStart=true
CleanWorkersOnExit=false
NestedWorkers=false
```

### 12. Ограничение по времени

```ini
Prefix=Z
Workers=4
MbPerWorker=256
ThreadsPerWorker=2
Seconds=60
DiskBurnMb=0
EnableWorkers=true
EnableReplication=false
MaxRunSeconds=3600
CleanWorkersOnExit=true
```

Родитель работает **1 час** и сам выходит.

### 13. Только диск, без картинок

```ini
Prefix=Z
Workers=2
MbPerWorker=1
ThreadsPerWorker=1
Seconds=60
DiskBurnMb=128
Waves=1
Fanout=1
EnableWorkers=true
EnableReplication=false
BurstMode=false
PicturesSource=none
CleanWorkersOnExit=true
MaxRunSeconds=0
```

**Что даёт:** чистая дисковая нагрузка. RAM почти не грузится (`MbPerWorker=1`). Картинки **не читаются** (`PicturesSource=none`). Диск пишет/читает 128 МБ × 2 дублёра.

### 14. Встроенные картинки для RAM

```ini
Prefix=Z
Workers=2
MbPerWorker=512
ThreadsPerWorker=1
Seconds=60
DiskBurnMb=0
Waves=1
Fanout=1
EnableWorkers=true
EnableReplication=false
PicturesSource=true
ImagesDir=images
MaxRunSeconds=0
```

**Что даёт:** RAM заполняется **встроенными картинками** из exe. Диск **не используется** (`DiskBurnMb=0`). Папка `images\` не нужна.

### 15. Картинки из папки с fallback

```ini
Prefix=Z
Workers=2
MbPerWorker=512
ThreadsPerWorker=1
Seconds=60
DiskBurnMb=0
Waves=1
Fanout=1
EnableWorkers=true
EnableReplication=false
PicturesSource=false
ImagesDir=images
MaxRunSeconds=0
```

**Что даёт:** картинки читаются из `images\` рядом с exe. Если папки нет — **fallback** на встроенные.

---

## Как остановить

Программа — это **дерево процессов**: родитель и его дублёры. Чтобы остановить всё, надо убить **всё дерево**, начиная с родителя. Если убить только детей, родитель тут же досыпет новых.

### Способ 0. Убрать автозапуск (первым делом)

Если было `AutoStart=true`, при перезагрузке программа вернётся. Убирай запись **сначала**.

**Через конфиг:**

```ini
AutoStart=false
```

**Через реестр:**

`Win+R` → `regedit` → перейди в `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`. Удали `ZAutoRun`.

**Через PowerShell:**

```powershell
Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' `
  -Name 'ZAutoRun' -ErrorAction SilentlyContinue
```

### Способ 1. Мягкая остановка (пауза)

```ini
EnableWorkers=false
StopParentWhenDisabled=false
```

Родитель через 2 секунды перестанет плодить новых. Старые доживут `Seconds`. Родитель **останется жив**, но спящий.

### Способ 2. Полная остановка через конфиг

```ini
EnableWorkers=false
StopParentWhenDisabled=true
```

Родитель **сам завершится** через 2 секунды. Уберёт автозапуск. Удалит `workers\` (если `CleanWorkersOnExit=true`).

### Способ 3. Через PowerShell

```powershell
Get-CimInstance Win32_Process | Where-Object { $_.Name -like 'Z*' } |
  ForEach-Object { taskkill /F /T /PID $_.ProcessId }
```

### Способ 4. По имени (cmd)

```cmd
taskkill /F /T /IM ZHost.exe
taskkill /F /T /IM ZRuntime.exe
taskkill /F /T /IM ZBroker.exe
taskkill /F /T /IM ZWorker.exe
taskkill /F /T /IM ZService.exe
taskkill /F /T /IM ZAgent.exe
taskkill /F /T /IM ZNode.exe
taskkill /F /T /IM ZCore.exe
```

### Способ 5. По пути (безопаснее)

Если префикс совпадает с системным (`svchost` и т.п.):

```powershell
Get-Process | Where-Object { $_.Path -like '*\workers\*' } | Stop-Process -Force
```

### Способ 6. Через диспетчер задач

1. `Ctrl+Shift+Esc` → **«Подробности»**.
2. Включи столбцы **«Путь к образу»** и **«Командная строка»**.
3. Найди процессы с путём в `workers\`.
4. **Сначала родитель** (без `--worker` в командной строке), потом дети.

### Способ 7. Крайний случай — перезагрузка

1. Убери автозапуск.
2. Перезагрузись.
3. **Не запускай exe.**
4. Удали рабочую папку и запись в реестре.

### Способ 8. Безопасный режим

1. Убери автозапуск через `regedit`.
2. Перезагрузись в **безопасном режиме** (Shift + Перезагрузка).
3. В safe mode автозапуск не срабатывает.
4. Удали рабочую папку и exe.
5. Перезагрузись.

### Удалить файлы

```cmd
rmdir /S /Q "C:\путь\к\рабочей\папке\workers"
rmdir /S /Q "C:\путь\к\рабочей\папке\diskiotmp"
rmdir /S /Q "C:\путь\к\рабочей\папке\images"
```

Или всю рабочую папку.

### Частые ошибки при остановке

- **Убить детей, оставить родителя.** Родитель досыпет новых. **Сначала родителя.**
- **Забыть автозапуск.** Перезагрузка вернёт всё.
- **Убить по имени системные процессы.** Если маскировались под `svchost` — фильтруй **по пути**.
- **`taskkill /IM` без `/T`.** Дети переживут родителя. **Всегда `/T`.**
- **Без прав администратора.** Запусти `cmd` от админа.
- **Несколько независимых родителей.** Убей всех сразу.

### Проверочный чек-лист

- [ ] `Get-Process Z*` — пусто.
- [ ] Нет процессов `ZHost`, `ZRuntime` и т.д.
- [ ] В реестре нет `ZAutoRun`.
- [ ] После перезагрузки не появляются.
- [ ] Папка `workers\` удалена.
- [ ] Папка `diskiotmp\` удалена (если была).
- [ ] exe удалён.
- [ ] `%LocalAppData%\StressTestCfg\` пуста (если был fallback).

### Ошибки `[clean] Не удалось удалить папку workers`

Это **не критичная ошибка**. Появляется, когда родитель пытается удалить `workers\`, но её **держат живые дублёры**. Они независимые процессы и доживают свой `Seconds`.

**Что делать:**

- Если `StopParentWhenDisabled=true` — родитель убивает дублёров перед выходом, папка удаляется сразу.
- Если `CleanWorkersOnExit=true` и дублёры ещё живы — папка удалится, когда последний умрёт. Или останется — удали вручную.

---

## Как найти свои процессы

### По префиксу

```powershell
Get-Process Z* | Select-Object Id, ProcessName, Path
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
  Where-Object { $_.CommandLine -like '*--prefix Z*' } |
  Select-Object ProcessId, Name, CommandLine | Format-Table -AutoSize
```

### В диспетчере задач

1. `Ctrl+Shift+Esc` → **«Подробности»**.
2. Включи столбцы **«Имя»**, **«Путь к образу»**, **«Командная строка»**.
3. Ищи путь в `workers\`.

**Почему во вкладке «Процессы» они без имени:** Windows группирует дерево, имя у корня. Смотри «Подробности» или PowerShell.

---

## Частые вопросы

**Почему процессов без имени в диспетчере?**
Во вкладке «Процессы» Windows группирует дерево. Смотри «Подробности».

**Конфиг не читается.**
Проверь, что `config.txt` в одной папке с exe (или в `%LocalAppData%\StressTestCfg\`). Точный путь — в окне уведомления.

**`stress.log` не создаётся.**
Так задумано. Лог пишется только при проблемах.

**`workers\` исчезла.**
Сработала `CleanWorkersOnExit=true`. Поставь `false`, чтобы сохранять.

**Диск не используется, хотя я не указывал `DiskBurnMb`.**
Так и задумано. **Дефолт `DiskBurnMb=0`.** Чтобы включить — явно укажи `DiskBurnMb > 0`.

**Картинки не читаются.**
Проверь `PicturesSource`. По умолчанию `embedded` — картинки берутся из exe. Если они не вшиты — будет пустой массив.

**Папка `images\` не используется.**
При `PicturesSource=true` или `embedded` она игнорируется. Поставь `false` или `maybetrue`.

**`[clean] Не удалось удалить папку workers`.**
Это нормально, если дублёры ещё живы. Подожди `Seconds` секунд или поставь `StopParentWhenDisabled=true`.

**Антивирус удаляет exe.**
Добавь папку в исключения Defender.

**После перезагрузки процессы вернулись.**
Был `AutoStart=true`. Убери запись из реестра.

**Программа не запускается, окно мелькает.**
Запусти из `cmd` — увидишь ошибку. Или проверь `CRASH.txt`.

**Как ограничить время работы?**
Поставь `MaxRunSeconds=3600` (1 час). По умолчанию `0` — без ограничения.

**Как остановить родителя при отключении дублёров?**
`EnableWorkers=false` + `StopParentWhenDisabled=true`.

---

## Ограничения

- Только Windows (`kernel32.dll`, `user32.dll`, реестр).
- Требует .NET 8 runtime, если собирали без `--self-contained`.
- Не работает на Linux/macOS.
- Маскировка косметическая (имя + описание). Путь остаётся в `workers\`, издатель — «Неизвестно».
- При `taskkill /F` очистка папки не срабатывает.
- `PicturesSource`, `ImagesDir`, `MaxRunSeconds`, `NotifyOnStart`, `CleanWorkersOnExit`, `NestedWorkers` — **не перечитываются** на ходу.
- Чтение картинок из папки — **один раз** при старте дублёра. Не отслеживается динамически.

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