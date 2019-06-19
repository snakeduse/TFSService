﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Gui.Helper;
using Microsoft.TeamFoundation.Common;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using TfsAPI.Constants;
using TfsAPI.Extentions;
using TfsAPI.Interfaces;

namespace Gui.ViewModels.Notifications
{
    /// <summary>
    /// Т.к. команды выполняются один раз, для нового выполнения требуется пересоздать этот объект
    /// </summary>
    public class NewResponsesBaloonViewModel : ItemsAssignedBaloonViewModel
    {
        private readonly ITfsApi _api;
        private readonly List<WorkItem> _reviews;
        private bool isBusy;

        public NewResponsesBaloonViewModel(List<WorkItem> responses,
            List<WorkItem> reviews,
            ITfsApi api,
            string title = null)
            : base(responses, title ?? Properties.Resources.AS_CodeReviewRequested)
        {
            _reviews = reviews;
            _api = api;

            CloseReviewes = ObservableCommand.FromAsyncHandler(OnCloseGoodLooking, OnCanCloseGoodLooking).ExecuteOnce();
            CloseOldReviewes = ObservableCommand.FromAsyncHandler(OnCloseOld, OnCanCloseOld).ExecuteOnce();
        }

        public ICommand CloseReviewes { get; }
        public ICommand CloseOldReviewes { get; }

        public bool IsBusy { get => isBusy; set => Set(ref isBusy, value); }


        private bool OnCanCloseGoodLooking()
        {
            return _reviews.Any(x => x.IsNotClosed());
        }

        private bool OnCanCloseOld()
        {
            var now = DateTime.Now;

            return OnCanCloseGoodLooking()
                   && _reviews.Any(x => IsOld(x.CreatedDate));
        }

        private async Task OnCloseOld()
        {
            IsBusy = true;

            await Task.Run(() => _api.CloseCompletedReviews((request, responses) =>
            {
                if (responses.IsNullOrEmpty()
                    || request.HasState(WorkItemStates.Closed))
                    return false;

                // Что-то нуждается в доработке
                if (responses.Any(x => x.HasClosedReason(WorkItems.ClosedStatus.NeedsWork)))
                {
                    Trace.WriteLine($"Can't close {request.Id}, responses need work");
                    return false;
                }

                return IsOld(request.CreatedDate);
            }));

            IsBusy = true;
        }

        private async Task OnCloseGoodLooking()
        {
            IsBusy = false;

            await Task.Run(() => _api.CloseCompletedReviews((request, responses) =>
            {
                if (responses.IsNullOrEmpty()
                    || request.HasState(WorkItemStates.Closed))
                    return false;

                return responses.All(x => x.HasClosedReason(WorkItems.ClosedStatus.LooksGood));
            }));

            IsBusy = false;
        }


        /// <summary>
        ///     Вынес для гибкости дальнейшего функционала
        /// </summary>
        /// <param name="createdDate">Дата создания запроса кода</param>
        /// <returns></returns>
        private bool IsOld(DateTime createdDate)
        {
            var now = DateTime.Now;

            // Считаю старым 100-дневные запросы кода
            return (now - createdDate).Duration() > TimeSpan.FromDays(100);
        }
    }
}