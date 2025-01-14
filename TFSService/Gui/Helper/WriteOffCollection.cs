﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using TfsAPI.Extentions;
using TfsAPI.Interfaces;

namespace Gui.Helper
{
    /// <summary>
    ///     Коллекция для работы с запланированным списанием. Управляет записью в TFS
    /// </summary>
    public class WriteOffCollection : ObservableCollection<WriteOff>
    {
        #region Public methods

        /// <summary>
        ///     Прошел час штатной работы программы
        /// </summary>
        /// <param name="id"></param>
        /// <param name="hours"></param>
        public void ScheduleWork(int id, int hours)
        {
            var item = new WriteOff(id, hours);
            Add(item);

            Trace.WriteLine(
                $"{nameof(WriteOffCollection)}.{nameof(ScheduleWork)}: Hour scheduled at {item.Time.ToShortTimeString()}");
        }

        /// <summary>
        ///     Проверяем, записали ли чекины от пользователя
        /// </summary>
        /// <param name="tfs"></param>
        public void SyncCheckins(ITfsApi tfs)
        {
            var checkins = tfs.GetWriteoffs(DateTime.Today, DateTime.Now);

            Trace.WriteLine($"{nameof(WriteOffCollection)}.{nameof(SyncCheckins)}: Founded {checkins.Count} changes");

            foreach (var checkin in checkins)
            {
                var id = checkin.Key.WorkItem.Id;
                var date = (DateTime) checkin.Key.Fields[CoreField.ChangedDate].Value;

                if (!this.Any(x => x.Time == date && x.Id == id))
                {
                    var userCheckIn = new WriteOff(id, checkin.Value, date);
                    Add(userCheckIn);

                    Trace.WriteLine($"{nameof(WriteOffCollection)}.{nameof(SyncCheckins)}: Detected new check-in, " +
                                    $"Id - {checkin.Key.WorkItem.Id}, Time - {date.ToShortTimeString()}");
                }
            }
        }

        /// <summary>
        ///     Сколько часов программа поставила в очередь
        /// </summary>
        /// <returns></returns>
        public int ScheduledTime()
        {
            return GetManual(this).Sum(x => x.Hours);
        }

        /// <summary>
        ///     Сколько сегодня было зачекинено пользователем
        /// </summary>
        /// <returns></returns>
        public int CheckinedTime()
        {
            return this.Where(x => x.Recorded && x.CreatedByUser).Sum(x => x.Hours);
        }

        /// <summary>
        ///     Очищаем предыдущие записи
        /// </summary>
        public void ClearPrevRecords()
        {
            RemoveAll(x => !x.Time.IsToday());
        }

        /// <summary>
        ///     Списываем запланированную работу
        /// </summary>
        /// <param name="api">TFS API</param>
        /// <param name="capacity">Кол-во рабочих часов в этом дне</param>
        public void CheckinScheduledWork(ITfsApi api, int capacity)
        {
            // Обновили историю чекинов
            SyncCheckins(api);

            // Обрезали, если вышли за предел кол-ва часов
            CutOffByCapacity(capacity);

            CheckinWork(api);
        }

        /// <summary>
        ///     Синхронизируем дневной плн списания времени. Кол-во списанного времени должно быть равно дневной норме.
        /// </summary>
        /// <param name="api"></param>
        /// <param name="capacity"></param>
        /// <param name="currentItem"></param>
        public void SyncDailyPlan(ITfsApi api, int capacity, Func<WorkItem> currentItem)
        {
            // Обновили историю чекинов
            SyncCheckins(api);

            // Обрезали, если вышли за предел кол-ва часов
            CutOffByCapacity(capacity);

            // сколько было списно пользователем
            var byUser = CheckinedTime();
            // сколько было списано
            var scheduled = ScheduledTime();

            var delta = capacity - byUser - scheduled;

            // Нужно распланировать ещё времени
            if (delta > 0)
            {
                var item = currentItem();

                // Если элемент нулёвый, считаем, что списание выключено
                if (item != null)
                {
                    ScheduleWork(item.Id, delta);
                }
            }

            CheckinWork(api);
        }

        #endregion

        #region Constructors

        public WriteOffCollection(IEnumerable<WriteOff> source)
            : base(source)
        {
        }

        public WriteOffCollection()
        {
        }

        #endregion

        #region Private

        /// <summary>
        ///     Записываем всю работу в TFS.
        ///     В случаем с чекином вчерашней работы, она записывается отдельно и не мешает
        ///     дневному кол-ву работы
        /// </summary>
        private void CheckinWork(ITfsApi tfs)
        {
            // Получили задачи на списание времени
            var manual = Merge(GetManual(this));

            // Нашли элементы одним запросом
            var items = tfs.FindById(manual.Select(x => x.Id));

            foreach (var toWrite in manual)
            {
                // какая-то ошибка, такого номера нет
                if (!items.ContainsKey(toWrite.Id))
                {
                    Trace.WriteLine(
                        $"{nameof(WriteOffCollection)}.{nameof(CheckinWork)}: Cannot find item {toWrite.Id}");
                    continue;
                }

                // Получили рабочий элемента
                var workItem = items[toWrite.Id];

                try
                {
                    // Записали время
                    var revision = tfs.WriteHours(workItem, (byte) toWrite.Hours, true);
                    // Удалили этот рабочий элемента
                    RemoveAll(x => x.Id == toWrite.Id);

                    // Не получилось запписать, ошибка
                    if (revision == null)
                    {
                        Trace.WriteLine(
                            $"{nameof(WriteOffCollection)}.{nameof(CheckinWork)}: Cannot write off hours of task {workItem.Id}");
                        continue;
                    }

                    var time = (DateTime) revision.Fields[CoreField.ChangedDate].Value;

                    Add(new WriteOff(revision.WorkItem.Id,
                        toWrite.Hours,
                        time,
                        // Если запись была запланирована сегодня, считаем это обычным
                        // чекином юзера
                        toWrite.Time.IsToday(),
                        true));
                }
                catch (Exception e)
                {
                    Trace.WriteLine(e);
                }
            }

            ClearPrevRecords();
        }

        /// <summary>
        ///     Обрезаю запланированную работу по чекинам пользователя
        ///     или по его дневному графику
        /// </summary>
        /// <param name="maxHoursPerDay"></param>
        private void CutOffByCapacity(int maxHoursPerDay)
        {
            // Сколько пользователь начекинил
            var alreadyRecorded = CheckinedTime();
            // сколько программа поставила в очередь
            var scheduled = ScheduledTime();

            Trace.WriteLine(
                $"{nameof(WorkItemCollection)}.{nameof(CutOffByCapacity)}: User wrote off {alreadyRecorded} " +
                $"hour(s), capacity is {maxHoursPerDay}");

            // Пользователь сам начекинил на дневной предел
            if (alreadyRecorded >= maxHoursPerDay)
            {
                Trace.WriteLine(
                    $"{nameof(WriteOffCollection)}.{nameof(CutOffByCapacity)}: User already riched the day limit");
                Clear();
                return;
            }

            // Сколько времени 
            var delta = maxHoursPerDay - alreadyRecorded - scheduled;

            // Уложились в предел
            if (delta >= 0)
            {
                Trace.WriteLine(
                    $"{nameof(WriteOffCollection)}.{nameof(CutOffByCapacity)}: Scheduled work don't overflow the day limit");
                return;
            }

            // Часы, которая программа поставила на ожидание
            // Сортирую по кол-ву часов, потом по убыванию времени
            var manual = GetManual(this)
                .OrderBy(x => x.Hours)
                .ThenByDescending(x => x.Time)
                .ToList();

            // Убираем запланнированные списания начиная с самых мелких по часам и поставленных в очередь позже всего
            while (delta < 0 && manual.Any())
            {
                var first = manual[0];

                manual.Remove(first);
                Remove(first);
                delta -= first.Hours;

                Trace.WriteLine(
                    $"{nameof(WorkItemCollection)}.{nameof(CutOffByCapacity)}: Deleted scheduled {first.Hours} hour(s)," +
                    $"workitem {first.Id}");
            }
        }

        /// <summary>
        ///     Удаляем все элементы по условию
        /// </summary>
        /// <param name="condition"></param>
        private void RemoveAll(Func<WriteOff, bool> condition)
        {
            var toRemove = this.Where(condition).ToList();
            foreach (var item in toRemove) Remove(item);
        }

        #endregion

        #region static

        /// <summary>
        ///     Мерджит чекины пользователя и запланированные программой
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        private static List<WriteOff> Merge(IList<WriteOff> source)
        {
            var result = new List<WriteOff>();

            void MergeByCondition(Func<WriteOff, bool> func)
            {
                foreach (var off in source.Where(func))
                {
                    var first = result.FirstOrDefault(x => x.Id == off.Id
                                                           && x.CreatedByUser == off.CreatedByUser
                                                           && x.Recorded == off.Recorded);

                    if (first != null)
                        first.Increase(off.Hours);
                    else
                        result.Add(off);
                }
            }

            MergeByCondition(x => x.CreatedByUser && x.Recorded);
            MergeByCondition(x => !x.CreatedByUser && !x.Recorded);


            return result;
        }

        /// <summary>
        ///     Возвращает список запланированных программой чекинов. Отсортированы от свежих к старым
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        private static List<WriteOff> GetManual(IList<WriteOff> source)
        {
            return source.Where(x => !x.Recorded && !x.CreatedByUser)
                .OrderByDescending(x => x.Time)
                .ToList();
        }

        #endregion
    }
}