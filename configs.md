# Примеры конфигов

Готовые шаблоны `config.txt` под разные задачи. Скопируйте нужный блок в `config.txt` рядом с exe и запустите программу.

Каждый конфиг самодостаточен: содержит все ключи, чтобы было понятно, что и зачем. Но в реальности можно указывать только то, что отличается от дефолта — остальное подтянется из кода.

**Важно:** с версии с `DiskBurnMb=0` по умолчанию — диск **не используется**, если ключ не указан. Для включения диска явно укажите `DiskBurnMb` > 0.

---

## Содержание

- [Как пользоваться](#как-пользоваться)
- [Шаблон 1. Проверка — «оно вообще работает?»](#шаблон-1-проверка--оно-вообще-работает)
- [Шаблон 2. Тихий фон](#шаблон-2-тихий-фон)
- [Шаблон 3. Лёгкая нагрузка](#шаблон-3-лёгкая-нагрузка)
- [Шаблон 4. Средняя нагрузка](#шаблон-4-средняя-нагрузка)
- [Шаблон 5. Тяжёлая нагрузка](#шаблон-5-тяжёлая-нагрузка)
- [Шаблон 6. Вспышки — атака/отдых](#шаблон-6-вспышки--атакаотдых)
- [Шаблон 7. Автозапуск с задержкой](#шаблон-7-автозапуск-с-задержкой)
- [Шаблон 8. Диск-шторм](#шаблон-8-диск-шторм)
- [Шаблон 9. Взрыв числа процессов](#шаблон-9-взрыв-числа-процессов)
- [Шаблон 10. Экстренная остановка (пауза)](#шаблон-10-экстренная-остановка-пауза)
- [Шаблон 11. Полная остановка](#шаблон-11-полная-остановка)
- [Шаблон 12. Только маскировка](#шаблон-12-только-маскировка)
- [Шаблон 13. Свой префикс](#шаблон-13-свой-префикс)
- [Шаблон 14. Один процесс для отладки](#шаблон-14-один-процесс-для-отладки)
- [Шаблон 15. Долгая стабильная нагрузка](#шаблон-15-долгая-стабильная-нагрузка)
- [Шаблон 16. Ограничение времени работы](#шаблон-16-ограничение-времени-работы)
- [Шаблон 17. Встроенные картинки для RAM](#шаблон-17-встроенные-картинки-для-ram)
- [Шаблон 18. Картинки из папки с fallback](#шаблон-18-картинки-из-папки-с-fallback)
- [Шаблон 19. Только диск, без картинок](#шаблон-19-только-диск-без-картинок)
- [Шаблон 20. Минимум — только обязательные ключи](#шаблон-20-минимум--только-обязательные-ключи)

---

## Как пользоваться

1. Создайте `config.txt` рядом с exe:
   ```cmd
   Svchost.exe --write-config
   ```
2. Откройте `config.txt` блокнотом.
3. Скопируйте нужный шаблон из этого файла.
4. Вставьте в `config.txt`, сохраните.
5. Запустите `Svchost.exe`.

Если в конфиге чего-то не хватает — берётся значение по умолчанию из кода. Если ключ неизвестен — программа пишет в `stress.log` и игнорирует.

Горячее перечитывание: изменения подхватываются раз в `ConfigReloadMs` мс (по умолчанию 2 секунды). **Но** `PicturesSource`, `ImagesDir`, `MaxRunSeconds`, `NotifyOnStart`, `CleanWorkersOnExit`, `NestedWorkers` — **не перечитываются** на ходу.

---

## Шаблон 1. Проверка — «оно вообще работает?»

**Задача:** убедиться, что программа запускается, читает конфиг, маскируется. Минимум нагрузки.

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
NotifyTimeoutSec=3
CleanWorkersOnExit=true
NestedWorkers=false
PicturesSource=none
ImagesDir=images
MaxRunSeconds=0
ConfigReloadMs=2000
```

**Что даёт:** 1 родитель + 1 дублёр. ~64 МБ RAM. 1 поток CPU. 10 секунд жизни. Диск не используется. Картинки не читаются.

**Для чего:** первый запуск, проверка сборки.

---

## Шаблон 2. Тихий фон

**Задача:** нагрузка почти незаметна.

```ini
Prefix=Z
Workers=1
SpawnDelayMs=100
WaveDelayMs=500
MbPerWorker=64
ThreadsPerWorker=1
Seconds=5
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
PicturesSource=none
MaxRunSeconds=0
```

---

## Шаблон 3. Лёгкая нагрузка

**Задача:** заметно в диспетчере, но не мешает.

```ini
Prefix=Z
Workers=2
SpawnDelayMs=20
WaveDelayMs=300
MbPerWorker=128
ThreadsPerWorker=1
Seconds=15
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
PicturesSource=embedded
MaxRunSeconds=0
```

**Что даёт:** 2 дублёра, ~256 МБ RAM, 2 потока CPU, лёгкая дисковая нагрузка (8 МБ).

---

## Шаблон 4. Средняя нагрузка

```ini
Prefix=Z
Workers=4
SpawnDelayMs=10
WaveDelayMs=200
MbPerWorker=256
ThreadsPerWorker=2
Seconds=30
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
```

**Что даёт:** 4 × (1 + 2) = 12 процессов. ~3 ГБ RAM. ~8 потоков CPU. Диск 16 МБ.

---

## Шаблон 5. Тяжёлая нагрузка

```ini
Prefix=Z
Workers=6
SpawnDelayMs=10
WaveDelayMs=100
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
```

**Что даёт:** 6 × (1 + 2 + 4) = 42 процесса. ~21 ГБ RAM. ~12 потоков CPU.

**Осторожно:** на машине с 16 ГБ уйдёт в swap.

---

## Шаблон 6. Вспышки — атака/отдых

```ini
Prefix=Z
Workers=4
SpawnDelayMs=20
WaveDelayMs=300
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
```

**Цикл:** 10 + 60 + 10 + 180 = 260 сек.

---

## Шаблон 7. Автозапуск с задержкой

```ini
Prefix=Z
Workers=4
SpawnDelayMs=20
WaveDelayMs=300
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
```

**Что даёт:** автозапуск, 10 минут тишины, потом цикл.

**Как остановить:** см. раздел «Как остановить» в README. Сначала убрать автозапуск.

---

## Шаблон 8. Диск-шторм

**Важно:** `DiskBurnMb` теперь **явно** указывается.

```ini
Prefix=Z
Workers=4
SpawnDelayMs=10
WaveDelayMs=100
MbPerWorker=64
ThreadsPerWorker=1
Seconds=60
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
```

**Что даёт:** 4 дублёра пишут/читают по 128 МБ. Постоянная нагрузка на I/O.

---

## Шаблон 9. Взрыв числа процессов

```ini
Prefix=Z
Workers=4
SpawnDelayMs=1
WaveDelayMs=20
MbPerWorker=64
ThreadsPerWorker=1
Seconds=30
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

**Что даёт:** 4 × (1 + 3 + 9 + 27) = **160 процессов**. ~10 ГБ RAM.

**Осторожно:** на слабой машине может привести к зависанию.

---

## Шаблон 10. Экстренная остановка (пауза)

**Задача:** остановить нагрузку, но оставить родителя живым (можно возобновить).

```ini
EnableWorkers=false
EnableReplication=false
StopParentWhenDisabled=false
BurstMode=false
AutoStart=false
```

**Что даёт:** родитель переходит в **паузу**. Нагрузка спадает. Старые дублёры доживают `Seconds`. Позже можно вернуть `EnableWorkers=true` — родитель возобновит.

---

## Шаблон 11. Полная остановка

**Задача:** родитель **сам завершается**.

```ini
EnableWorkers=false
EnableReplication=false
StopParentWhenDisabled=true
BurstMode=false
AutoStart=false
CleanWorkersOnExit=true
```

**Что даёт:** через 2 секунды родитель выходит, убивает живых дублёров, убирает автозапуск, пытается удалить `workers\`.

---

## Шаблон 12. Только маскировка

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

**Что даёт:** один процесс `ZHost.exe` из `workers\`, висит, ничего не ест.

---

## Шаблон 13. Свой префикс

```ini
Prefix=MyApp
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
NotifyOnStart=true
CleanWorkersOnExit=true
NestedWorkers=false
PicturesSource=embedded
MaxRunSeconds=0
```

**Что даёт:** процессы `MyAppHost`, `MyAppRuntime` и т.д.

**Правила для префикса:** буквы, цифры, подчёркивание. Без пробелов, кавычек, слэшей.

---

## Шаблон 14. Один процесс для отладки

```ini
Prefix=Z
Workers=1
SpawnDelayMs=0
WaveDelayMs=0
MbPerWorker=64
ThreadsPerWorker=1
Seconds=60
DiskBurnMb=0
Waves=1
Fanout=1
EnableWorkers=false
EnableReplication=false
BurstMode=false
AutoStart=false
NotifyOnStart=true
CleanWorkersOnExit=false
NestedWorkers=false
PicturesSource=none
MaxRunSeconds=0
```

**Что даёт:** родитель запускается, но никого не плодит. Работает под своим именем.

---

## Шаблон 15. Долгая стабильная нагрузка

```ini
Prefix=Z
Workers=3
SpawnDelayMs=10
WaveDelayMs=200
MbPerWorker=256
ThreadsPerWorker=2
Seconds=300
DiskBurnMb=16
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
```

**Что даёт:** 3 дублёра, ~768 МБ RAM, ~6 потоков CPU. Каждый по 5 минут.

---

## Шаблон 16. Ограничение времени работы

```ini
Prefix=Z
Workers=4
SpawnDelayMs=20
WaveDelayMs=300
MbPerWorker=256
ThreadsPerWorker=2
Seconds=60
DiskBurnMb=16
Waves=1
Fanout=1
EnableWorkers=true
EnableReplication=false
BurstMode=false
AutoStart=false
NotifyOnStart=true
CleanWorkersOnExit=true
NestedWorkers=false
PicturesSource=embedded
MaxRunSeconds=3600
```

**Что даёт:** работает **1 час**, потом родитель сам выходит. `MaxRunSeconds` — секунды.

**Примеры:** `30` = 30 сек, `300` = 5 мин, `3600` = 1 час, `86400` = 24 часа.

---

## Шаблон 17. Встроенные картинки для RAM

**Задача:** заполнить RAM картинками, вшитыми в exe. Диск не трогать.

```ini
Prefix=Z
Workers=2
SpawnDelayMs=20
WaveDelayMs=300
MbPerWorker=512
ThreadsPerWorker=1
Seconds=60
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
PicturesSource=true
ImagesDir=images
MaxRunSeconds=0
```

**Что даёт:** RAM заполняется встроенными картинками. Диск не используется. Папка `images\` **не нужна**.

---

## Шаблон 18. Картинки из папки с fallback

**Задача:** читать картинки из `images\`, но если её нет — использовать встроенные.

```ini
Prefix=Z
Workers=2
SpawnDelayMs=20
WaveDelayMs=300
MbPerWorker=512
ThreadsPerWorker=1
Seconds=60
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
PicturesSource=false
ImagesDir=images
MaxRunSeconds=0
```

**Что даёт:** картинки из папки `images\` рядом с exe. Если папки нет — **fallback** на встроенные. Если и встроенных нет — пустой массив + `stress.log`.

---

## Шаблон 19. Только диск, без картинок

**Задача:** чистая дисковая нагрузка, без чтения картинок.

```ini
Prefix=Z
Workers=2
SpawnDelayMs=50
WaveDelayMs=500
MbPerWorker=1
ThreadsPerWorker=1
Seconds=60
DiskBurnMb=128
Waves=1
Fanout=1
EnableWorkers=true
EnableReplication=false
BurstMode=false
AutoStart=false
NotifyOnStart=true
NotifyTimeoutSec=3
CleanWorkersOnExit=true
NestedWorkers=false
PicturesSource=none
MaxRunSeconds=0
```

**Что даёт:** 2 дублёра пишут/читают по 128 МБ. RAM почти не грузится (`MbPerWorker=1`). Картинки не читаются (`PicturesSource=none`).

**Для чего:** тест диска.

---

## Шаблон 20. Минимум — только обязательные ключи

```ini
Prefix=Z
Workers=2
MbPerWorker=256
EnableReplication=false
AutoStart=false
```

**Что даёт:**

- `Prefix=Z` — приставка.
- `Workers=2` — 2 дублёра.
- `MbPerWorker=256` — 256 МБ на каждого.
- Остальное — из кода.

**Из кода подтянется:** `ThreadsPerWorker=2`, `Seconds=60`, `Waves=3`, `Fanout=2`, **`DiskBurnMb=0`** (диск выключен), `PicturesSource=embedded`, `MaxRunSeconds=0`, `AutoStart=false`.

**Рекомендация:** для экспериментов используй полный шаблон, а когда поймёшь, что нужно — сократи до минимума.

---

## Сводная таблица шаблонов

| № | Название | Workers | RAM (МБ) | CPU | Диск | Процессов |
|---|---|---|---|---|---|---|
| 1 | Проверка | 1 | 64 | 1 | — | ~2 |
| 2 | Тихий фон | 1 | 64 | 1 | — | ~2 |
| 3 | Лёгкая | 2 | 128 | 1 | 8 | ~3 |
| 4 | Средняя | 4 | 256 | 2 | 16 | 12 |
| 5 | Тяжёлая | 6 | 512 | 2 | 32 | 42 |
| 6 | Вспышки | 4 | 512 | 2 | 16 | 4 |
| 7 | Автозапуск | 4 | 512 | 2 | 32 | 12+ |
| 8 | Диск-шторм | 4 | 64 | 1 | 128 | 4 |
| 9 | Взрыв | 4 | 64 | 1 | — | 160 |
| 10 | Пауза | — | — | — | — | 0 |
| 11 | Полная остановка | — | — | — | — | 0 |
| 12 | Маскировка | 0 | 0 | 0 | — | 1 |
| 13 | Свой префикс | 2 | 128 | 1 | — | ~3 |
| 14 | Отладка | 0 | 64 | 1 | — | 1 |
| 15 | Долгая | 3 | 256 | 2 | 16 | 3 |
| 16 | Ограничение | 4 | 256 | 2 | 16 | 4 |
| 17 | Встроенные картинки | 2 | 512 | 1 | — | 2 |
| 18 | Картинки из папки | 2 | 512 | 1 | — | 2 |
| 19 | Только диск | 2 | 1 | 1 | 128 | 2 |
| 20 | Минимум | 2 | 256 | 2 | — | ~4 |

---

## Как менять параметры по ситуации

**Хочу быстрее нагрузку:**

- `Workers` ↑
- `MbPerWorker` ↑
- `ThreadsPerWorker` ↑
- `Waves` ↑, `Fanout` ↑ (с `EnableReplication=true`)
- `DiskBurnMb` > 0 (иначе диск не используется!)

**Хочу тише:**

- `Workers` ↓
- `MbPerWorker` ↓
- `ThreadsPerWorker` ↓
- `EnableReplication=false`
- `DiskBurnMb=0`

**Хочу циклы:**

- `BurstMode=true`
- `AttackSec`, `RestSec` — под задачу.

**Хочу автозапуск:**

- `AutoStart=true`
- Не забыть потом остановить.

**Хочу картинки из exe:**

- `PicturesSource=true`
- Убедиться, что в `pictures\` в проекте есть файлы и `.csproj` их вшивает.

**Хочу картинки из папки:**

- `PicturesSource=false`
- Положить картинки в `images\` рядом с exe.

**Хочу и то, и другое:**

- `PicturesSource=maybetrue`

**Хочу ограничить время:**

- `MaxRunSeconds=3600` (1 час).

**Хочу, чтобы родитель выходил при остановке:**

- `EnableWorkers=false` + `StopParentWhenDisabled=true`.

---

## Чего не делать

- **`Workers=0`** — обрежется до 1.
- **`MbPerWorker=0`** — обрежется до 1.
- **`Prefix=` (пусто)** — берётся дефолт `Z`.
- **`Prefix` с пробелами и кавычками** — не работает.
- **`Waves=4, Fanout=3`** без понимания — это 160+ процессов.
- **`MbPerWorker=2048, Workers=8`** на машине с 8 ГБ RAM — swap и зависание.
- **`DiskBurnMb=512`** без запаса места — забитый диск.
- **`AutoStart=true`** без плана остановки.
- **Кириллица в именах ключей** — не работает.
- **Ожидать, что диск используется без `DiskBurnMb`** — теперь по умолчанию диск **выключен**.
- **Ожидать, что `images\` читается при `PicturesSource=true`** — не читается.
- **Ожидать, что `MaxRunSeconds` перечитывается на ходу** — не перечитывается.

---

## Быстрая проверка после смены конфига

**Проверить, что программа видит конфиг:**

1. Запусти exe.
2. В окне уведомления смотри `Конфиг: <путь>`.
3. Сверь с путём, где сохранил `config.txt`.

**Проверить, что параметры применились:**

1. В окне уведомления смотри `Дублёров`, `RAM`, `CPU-потоков`.
2. Сверь с конфигом.

**Проверить на ходу:**

1. Измени `Workers` в конфиге.
2. Сохрани.
3. Подожди `ConfigReloadMs` мс (2 сек).
4. Проверь:

   ```powershell
   (Get-CimInstance Win32_Process | Where-Object { $_.Name -like 'Z*' } | Measure-Object).Count
   ```

Число процессов должно измениться.

**Проверить, что диск используется:**

- Смотри папку `diskiotmp\` рядом с exe. Если она есть и там файлы `io_*.tmp` — диск работает.
- В диспетчере задач → «Производительность» → «Диск» → «Активное время».

**Проверить, что картинки используются:**

- Смотри `stress.log`. Если там `[worker 0] Встроенных картинок: N шт.` или `Картинок из папки: N шт.` — картинки читаются.
- Или смотри `resmon` → «Память» → «Рабочий набор» процесса. Если ≈ `MbPerWorker` — буфер занят.

---

## Ссылки

- [README.md](README.md) — основная документация.
- Раздел «Параметры конфига» — полный список ключей.
- Раздел «Как остановить» — способы остановки.
- Раздел «Заполнение памяти картинками» — про `PicturesSource`, `ImagesDir`, `pictures\`, `images\`.