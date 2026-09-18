# Примеры конфигов

Готовые шаблоны `config.txt` под разные задачи. Скопируйте нужный блок в `config.txt` рядом с exe и запустите программу.

Каждый конфиг самодостаточен: содержит все ключи, чтобы было понятно, что и зачем. Но в реальности можно указывать только то, что отличается от дефолта — остальное подтянется из кода.

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
- [Шаблон 10. Экстренная остановка](#шаблон-10-экстренная-остановка)
- [Шаблон 11. Только маскировка](#шаблон-11-только-маскировка)
- [Шаблон 12. Свой префикс — легко найти процессы](#шаблон-12-свой-префикс--легко-найти-процессы)
- [Шаблон 13. Один процесс для отладки](#шаблон-13-один-процесс-для-отладки)
- [Шаблон 14. Долгая стабильная нагрузка](#шаблон-14-долгая-стабильная-нагрузка)
- [Шаблон 15. Минимум — только обязательные ключи](#шаблон-15-минимум--только-обязательные-ключи)

---

## Как пользоваться

1. Создайте `config.txt` рядом с exe:
   ```cmd
   StressTest.exe --write-config
   ```
2. Откройте `config.txt` блокнотом.
3. Скопируйте нужный шаблон из этого файла.
4. Вставьте в `config.txt`, сохраните.
5. Запустите `StressTest.exe`.

Если в конфиге чего-то не хватает — берётся значение по умолчанию из кода. Если ключ неизвестен — программа пишет в `stress.log` и игнорирует.

Горячее перечитывание: изменения подхватываются раз в `ConfigReloadMs` мс (по умолчанию 2 секунды).

---

## Шаблон 1. Проверка — «оно вообще работает?»

**Задача:** убедиться, что программа запускается, читает конфиг, маскируется. Минимум нагрузки.

```ini
# ---- Имена процессов ----
Prefix=ZX

# ---- Пул ----
Workers=1
SpawnDelayMs=50
WaveDelayMs=500

# ---- Нагрузка ----
MbPerWorker=64
ThreadsPerWorker=1
Seconds=10
DiskBurnMb=0

# ---- Репликация ----
Waves=1
Fanout=1
EnableReplication=false

# ---- Управление ----
EnableWorkers=true

# ---- Burst ----
BurstMode=false

# ---- Автозапуск ----
AutoStart=false

# ---- Уведомление ----
NotifyOnStart=true

# ---- Очистка ----
CleanWorkersOnExit=true
NestedWorkers=false

# ---- Служебное ----
ConfigReloadMs=2000
```

**Что даёт:**
- 1 родитель + 1 дублёр. Всего ~2-3 процесса.
- ~64 МБ RAM, 1 поток CPU.
- 10 секунд жизни.
- Уведомление появляется и закрывается.

**Для чего:** первый запуск, проверка, что всё собралось и работает.

---

## Шаблон 2. Тихий фон

**Задача:** нагрузка почти незаметна, не мешает работать.

```ini
Prefix=ZX
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
ConfigReloadMs=2000
```

**Что даёт:** 1 дублёр, 64 МБ, 1 поток, 5 секунд жизни. В диспетчере есть, но машина не замечает.

**Для чего:** фоновая нагрузка на 1-2%, «чтобы что-то было».

---

## Шаблон 3. Лёгкая нагрузка

**Задача:** заметно в диспетчере, но не мешает.

```ini
Prefix=ZX
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
ConfigReloadMs=2000
```

**Что даёт:** 2 дублёра, ~256 МБ RAM, 2 потока CPU, лёгкая дисковая нагрузка (8 МБ на дублёра).

**Для чего:** повседневная фоновая нагрузка.

---

## Шаблон 4. Средняя нагрузка

**Задача:** машина шумит, вентиляторы крутятся, но система отзывается.

```ini
Prefix=ZX
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
ConfigReloadMs=2000
```

**Что даёт:** 4 × (1 + 2) = 12 процессов. ~3 ГБ RAM. ~8 потоков CPU. Диск.

**Для чего:** «средне». Ощутимо, но без фанатизма.

---

## Шаблон 5. Тяжёлая нагрузка

**Задача:** серьёзный стресс. Хорошо для теста охлаждения.

```ini
Prefix=ZX
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
ConfigReloadMs=2000
```

**Что даёт:** 6 × (1 + 2 + 4) = 42 процесса. ~21 ГБ RAM. ~12 потоков CPU. Диск 32 МБ.

**Осторожно:** на машине с 16 ГБ уйдёт в swap. Только для мощных ПК или тестов «как оно ломается».

---

## Шаблон 6. Вспышки — атака/отдых

**Задача:** не постоянная нагрузка, а циклами — атака, потом тишина.

```ini
Prefix=ZX
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

# ---- Burst ----
BurstMode=true
AttackSec=60
RestSec=180
RampUpSec=10
RampDownSec=10
BurstStartDelaySec=0

# ---- Прочее ----
AutoStart=false
NotifyOnStart=false
CleanWorkersOnExit=true
NestedWorkers=false
ConfigReloadMs=2000
```

**Что даёт:** цикл = 10 + 60 + 10 + 180 = 260 сек. Атака длится 60 сек, потом 180 сек тишины.

**Для чего:** если хочешь «нагрузку периодами», а не постоянно.

---

## Шаблон 7. Автозапуск с задержкой

**Задача:** стартовать при входе в систему, но начать нагрузку не сразу.

```ini
Prefix=ZX
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

# ---- Burst ----
BurstMode=true
AttackSec=180
RestSec=300
RampUpSec=20
RampDownSec=20
BurstStartDelaySec=600

# ---- Автозапуск ----
AutoStart=true

# ---- Прочее ----
NotifyOnStart=false
CleanWorkersOnExit=false
NestedWorkers=false
ConfigReloadMs=2000
```

**Что даёт:**
- Программа стартует при входе в систему.
- Первые 10 минут (600 сек) ничего не делает.
- Потом цикл: 20 + 180 + 20 + 300 = 520 сек.
- В атаке — полная нагрузка, потом отдых.

**Для чего:** если хочешь, чтобы программа работала в фоне, но не мешала при старте Windows.

**Как остановить:** см. раздел «Как остановить» в README. Обязательно сначала убрать автозапуск.

---

## Шаблон 8. Диск-шторм

**Задача:** нагрузка на диск (HDD/SSD), CPU и RAM минимальны.

```ini
Prefix=ZX
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
ConfigReloadMs=2000
```

**Что даёт:** 4 дублёра пишут и читают по 128 МБ. Постоянная нагрузка на I/O.

**Осторожно:**
- На HDD — головки не успевают, машина подтормаживает.
- На SSD — нагрев контроллера.
- Свободное место: до 4 × 128 = 512 МБ занято временно (файлы перезаписываются по кругу).

**Для чего:** тест диска.

---

## Шаблон 9. Взрыв числа процессов

**Задача:** максимальное число процессов при минимуме ресурсов на каждый.

```ini
Prefix=ZX
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
ConfigReloadMs=1000
```

**Что даёт:** 4 × (1 + 3 + 9 + 27) = **160 процессов**. ~10 ГБ RAM. Каждый по 1 потоку CPU.

**Для чего:** тест планировщика ОС, тест нагрузки на таблицу процессов.

**Осторожно:** на слабой машине может привести к жёсткому зависанию.

---

## Шаблон 10. Экстренная остановка

**Задача:** быстро остановить уже работающую программу.

```ini
Prefix=ZX
EnableWorkers=false
EnableReplication=false
BurstMode=false
AutoStart=false
```

**Что даёт:** родитель перечитает конфиг через 2 секунды и перестанет создавать новых дублёров. Старые доживут свой `Seconds` и умрут.

**Как использовать:**
1. Откройте работающий `config.txt`.
2. Замените содержимое на этот блок.
3. Сохраните.
4. Через 2-5 секунд нагрузка начнёт спадать.

**Что не решает:** родитель останется висеть. Его надо убить отдельно (`taskkill /F /T /IM ZXHost.exe`).

---

## Шаблон 11. Только маскировка

**Задача:** проверить, как работает маскировка, без нагрузки.

```ini
Prefix=ZX
EnableWorkers=false
EnableReplication=false
BurstMode=false
AutoStart=false
NotifyOnStart=true
CleanWorkersOnExit=false
NestedWorkers=false
```

**Что даёт:** один процесс `ZXHost.exe` из папки `workers\`, висит в фоне, ничего не ест.

**Для чего:** тест маскировки. Убедиться, что имя, путь, описание — как надо.

---

## Шаблон 12. Свой префикс — легко найти процессы

**Задача:** поменять имена процессов на свои, чтобы легко находить.

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
ConfigReloadMs=2000
```

**Что даёт:** процессы называются `MyAppHost`, `MyAppRuntime` и т.д. Искать через:

```powershell
Get-Process MyApp*
```

**Правила для префикса:**
- Буквы, цифры, подчёркивание.
- Без пробелов, кавычек, слэшей.
- Латиница удобнее кириллицы.

**Для чего:** персонализация, тесты, различение нескольких запусков.

---

## Шаблон 13. Один процесс для отладки

**Задача:** запустить без репликации и маскировки — для отладки в VS.

```ini
Prefix=ZX
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
ConfigReloadMs=2000
```

**Что даёт:** родитель запускается, читает конфиг, но никого не плодит. Маскировка отключена флагом `EnableWorkers=false` — родитель работает под своим именем.

**Для чего:** отладка логики без сложностей с маскировкой.

---

## Шаблон 14. Долгая стабильная нагрузка

**Задача:** постоянная нагрузка в течение нескольких часов.

```ini
Prefix=ZX
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
ConfigReloadMs=2000
```

**Что даёт:** 3 дублёра, ~768 МБ RAM, ~6 потоков CPU. Каждый живёт по 5 минут, потом заменяется.

**Для чего:** стабильная нагрузка для теста охлаждения на длительное время.

---

## Шаблон 15. Минимум — только обязательные ключи

**Задача:** указать только то, что отличается от дефолта. Остальное подтянется из кода.

```ini
Prefix=ZX
Workers=2
MbPerWorker=256
EnableReplication=false
AutoStart=false
```

**Что даёт:**
- `Prefix=ZX` — приставка.
- `Workers=2` — 2 дублёра.
- `MbPerWorker=256` — 256 МБ на каждого.
- Остальное — из кода (`ThreadsPerWorker=2`, `Seconds=60`, `Waves=3`, `Fanout=2`, `DiskBurnMb=16` и т.д.).

**Для чего:** если хочешь быстро поменять пару параметров, не расписывая всё.

**Важно:** так как `EnableReplication=false` в коде — репликация выключена. Но `Waves=3, Fanout=2` из кода всё равно применятся, если репликацию включить.

**Рекомендация:** для экспериментов используй полный шаблон (например, № 4), а когда поймёшь, что нужно — сократи до минимума.

---

## Сводная таблица шаблонов

| № | Название | Workers | RAM (МБ) | CPU | Процессов | Нагрузка |
|---|---|---|---|---|---|---|
| 1 | Проверка | 1 | 64 | 1 | ~2 | минимум |
| 2 | Тихий фон | 1 | 64 | 1 | ~2 | минимум |
| 3 | Лёгкая | 2 | 128 | 1 | ~3 | лёгкая |
| 4 | Средняя | 4 | 256 | 2 | 12 | средняя |
| 5 | Тяжёлая | 6 | 512 | 2 | 42 | тяжёлая |
| 6 | Вспышки | 4 | 512 | 2 | 4 | циклами |
| 7 | Автозапуск | 4 | 512 | 2 | 12+ | циклами |
| 8 | Диск-шторм | 4 | 64 | 1 | 4 | I/O |
| 9 | Взрыв | 4 | 64 | 1 | 160 | процессов |
| 10 | Стоп | — | — | — | — | выключено |
| 11 | Маскировка | 0 | 0 | 0 | 1 | нет |
| 12 | Свой префикс | 2 | 128 | 1 | ~3 | лёгкая |
| 13 | Отладка | 0 | 64 | 1 | 1 | нет |
| 14 | Долгая | 3 | 256 | 2 | 3 | средняя |
| 15 | Минимум | 2 | 256 | 2 | ~4 | лёгкая |

---

## Как менять параметры по ситуации

**Хочу быстрее нагрузку:**

- `Workers` ↑
- `MbPerWorker` ↑
- `ThreadsPerWorker` ↑
- `Waves` ↑, `Fanout` ↑ (с `EnableReplication=true`)

**Хочу тише:**

- `Workers` ↓
- `MbPerWorker` ↓
- `ThreadsPerWorker` ↓
- `EnableReplication=false`
- `DiskBurnMb=0`

**Хочу циклы:**

- `BurstMode=true`
- `AttackSec` и `RestSec` подобрать под задачу.

**Хочу автозапуск:**

- `AutoStart=true`
- Не забыть потом остановить через README.

**Хочу свой префикс:**

- `Prefix=MyName`
- Проверить, что буквы/цифры/подчёркивание.

---

## Чего не делать

- **`Workers=0`** — обрежется до 1.
- **`MbPerWorker=0`** — обрежется до 1.
- **`Prefix=` (пусто)** — берётся дефолт `ZX`.
- **`Prefix` с пробелами и кавычками** — не работает.
- **`Waves=4, Fanout=3`** без понимания, что это 160+ процессов.
- **`MbPerWorker=2048, Workers=8`** на машине с 8 ГБ RAM — уйдёт в swap и повесит систему.
- **`DiskBurnMb=512`** без запаса свободного места — может забить диск.
- **`AutoStart=true`** без плана, как потом остановить.
- **Ключи с кириллицей в имени** — не работают (`Воркерс=4` — не пройдёт). Кириллица допустима только в **значениях** строк.

---

## Быстрая проверка после смены конфига

**Проверить, что программа видит конфиг:**

1. Запустите exe.
2. В окне уведомления смотрите строку `Конфиг: <путь>`.
3. Сверьте с путём, где вы сохранили `config.txt`.

**Проверить, что параметры применились:**

1. В окне уведомления посмотрите `Дублёров: N`, `RAM: N МБ`, `CPU-потоков: N`.
2. Сверьте с вашим конфигом.

**Проверить на ходу:**

1. Измените `Workers` в конфиге.
2. Сохраните.
3. Подождите `ConfigReloadMs` мс (по умолчанию 2 сек).
4. Проверьте через PowerShell:

   ```powershell
   (Get-CimInstance Win32_Process | Where-Object { $_.Name -like 'ZX*' } | Measure-Object).Count
   ```

Число процессов должно измениться.

---

## Ссылки

- [README.md](README.md) — основная документация.
- Раздел «Параметры конфига» в README — полный список ключей.
- Раздел «Как остановить» в README — способы остановки.