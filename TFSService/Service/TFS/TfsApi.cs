﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Framework.Client;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.TeamFoundation.VersionControl.Common;
using Microsoft.TeamFoundation.WorkItemTracking.Client;

namespace Service.TFS
{
    public class TfsApi : IDisposable
    {
        #region Consts

        private const string WORKITEMS_TABLE_NAME = "WorkItems";

        private const string WORKITEM_TYPE_FIELD = "[Work Item Type]";
        private const string ASSIGNED_TO_ME = "[Assigned To] = @me";
        private const string STATE_FIELD = "[State]";

        private const string COMPLITED_FIELD = "Completed Work";
        private const string REMAINING_FIELD = "Remaining Work";

        #endregion

        #region Fields
        
        private readonly TfsTeamProjectCollection _project;
        private readonly VersionControlServer _versionControl;
        private readonly WorkItemStore _itemStore;

        #endregion

        #region Events

        public event EventHandler<CommitCheckinEventArgs> Checkin;

        #endregion

        public TfsApi(string url)
        {
            _project = new TfsTeamProjectCollection(new Uri(url));
            _versionControl = _project.GetService<VersionControlServer>();
            _itemStore = _project.GetService<WorkItemStore>();

            Subscribe();
        }

        #region Public Methods

        /// <summary>
        /// Возвращает последние чекины за указанный промежуток времени
        /// </summary>
        public IList<Changeset> GetLatestCheckIns(TimeSpan fromNow)
        {
            var parameters = new QueryHistoryParameters("*", RecursionType.Full)
            {
                Author = _project.AuthorizedIdentity.DisplayName,
            };

            var lastDate = DateTime.Now - fromNow.Duration();
            var changes = _versionControl.QueryHistory(parameters).Where(x => x.CreationDate >= lastDate);

            return changes.ToList();
        }

        /// <summary>
        /// Возвращает список незакрытых тасков, которые сейчас находятся на мне
        /// </summary>
        /// <returns></returns>
        public IList<WorkItem> GetMyTasks()
        {
            var items = _itemStore.Query(
                $"SELECT * from {WORKITEMS_TABLE_NAME} " +
                $"WHERE {WORKITEM_TYPE_FIELD} = 'Task' " +
                $"AND {ASSIGNED_TO_ME} " +
                $"AND {STATE_FIELD} != 'Closed'");

            var result = new List<WorkItem>();

            foreach (WorkItem item in items)
            {
                result.Add(item);
            }

            return result;
        }

        /// <summary>
        /// Списывает часы в опр. таск
        /// </summary>
        /// <param name="task"></param>
        /// <param name="hours"></param>
        /// <exception cref="ArgumentNullException">
        /// <para>Если task == null,либо тип элемента не Task, либо таск не активен</para>
        /// <para>Если кол-во часов, которые надо списать равны нулю</para></exception>
        public void WriteHours(WorkItem task, uint hours)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));

            if (hours == 0)
                throw new ArgumentException("hours should be more than one");

            if (task.Type.Name != "Task")
                throw new ArgumentException("task should have Task type");

            if (task.State != "Active")
                throw new ArgumentException("Cannot add hours to task that is not active");

            var workField = task.Fields[COMPLITED_FIELD];
            var remainingField = task.Fields[REMAINING_FIELD];

            var total = int.Parse(workField.Value.ToString()) + hours;
            var remain = Math.Max(int.Parse(remainingField.Value.ToString()) - hours, 0);
            
        }

        public void Dispose()
        {
            Unsubscribe();
            _project.Dispose();
        }

        #endregion

        #region Private Methods

        private void Subscribe()
        {
            _versionControl.CommitCheckin += FireCheckinEvent;
        }

        private void Unsubscribe()
        {
            _versionControl.CommitCheckin -= FireCheckinEvent;
        }

        private void FireCheckinEvent(object sender, CommitCheckinEventArgs e)
        {
            Checkin?.Invoke(sender, e);
        }

        #endregion
    }
}