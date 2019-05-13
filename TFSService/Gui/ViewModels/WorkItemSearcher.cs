﻿using System.Collections.Generic;
using System.Linq;
using Gui.Helper;
using Microsoft.TeamFoundation.Common;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using Microsoft.VisualStudio.Services.Common;
using TfsAPI;

namespace Gui.ViewModels
{
    class WorkItemSearcher : BindableExtended
    {
        #region Fields

        private readonly ITfs _api;
        private readonly TimedAction<string, IList<WorkItemVm>> _action;

        private WorkItemVm _selected;
        private IList<WorkItemVm> _items;
        private string _text;

        #endregion

        /// <summary>
        /// Базовый конструктор
        /// </summary>
        /// <param name="api"></param>
        public WorkItemSearcher(ITfs api)
        {
            _api = api;
            _action = new TimedAction<string, IList<WorkItemVm>>(PerformSearch);
            _action.Performed += OnResult;

            _items = new List<WorkItemVm>();
        }

        /// <summary>
        /// Заполняем выпадающий список элементами, привязанными на меня
        /// </summary>
        /// <param name="api"></param>
        /// <param name="types">Типы элементов, который хочу вывести. Cм. <see cref="WorkItemTypes"/></param>
        public WorkItemSearcher(ITfs api, params string[] types)
            : this(api)
        {
            var mine = _api.GetMyWorkItems();

            if (!types.IsNullOrEmpty())
            {
                mine = mine.Where(x => types.Contains(x.Type.Name)).ToList();
            }

            foreach (var item in mine)
            {
                _items.Add(item);
            }
        }

        #region Propeties

        public string Text
        {
            get => _text;
            set
            {
                if (SetProperty(ref _text, value))
                {
                    TryScheduleSearch(Text);
                }
            }
        }

        public WorkItemVm Selected
        {
            get => _selected;
            set => SetProperty(ref _selected, value);
        }

        public IList<WorkItemVm> Items
        {
            get => _items;
            set => SetProperty(ref _items, value);
        }

        #endregion

        #region Private Methods

        private void TryScheduleSearch(string text)
        {
            // Пустая строчка
            if (string.IsNullOrWhiteSpace(text))
                return;

            // Такой элемент уже есть, искать не надо
            if (int.TryParse(text, out var id) && Items.Any(x => x.Item.Id == id))
                return;

            _action.Shedule(text);
        }

        private IList<WorkItemVm> PerformSearch(string arg)
        {
            var list = new List<WorkItemVm>();

            // Сначала ищем по ID
            if (int.TryParse(arg, out var id)
             && _api.FindById(id) is WorkItem find)
            {
                list.Add(find);
            }
            // Не получилось - ищем по строке
            else
            {
                var finded = _api.Search(arg);
                foreach (var item in finded)
                {
                    list.Add(item);
                }
            }

            return list;
        }

        private void OnResult(object sender, IList<WorkItemVm> e)
        {
            Items = e;
        }

        #endregion
    }
}