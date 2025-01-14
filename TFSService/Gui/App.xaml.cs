﻿using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using Gui.Helper;
using Gui.View;
using Gui.ViewModels;
using Gui.ViewModels.Notifications;
using TfsAPI.Interfaces;
using TfsAPI.TFS;
using ToastNotifications;
using ToastNotifications.Core;
using ToastNotifications.Lifetime;
using ToastNotifications.Messages;
using ToastNotifications.Position;
using Calendar = System.Windows.Controls.Calendar;

namespace Gui
{
    /// <summary>
    ///     Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            DispatcherUnhandledException += WriteEx;

            var listener = new TextWriterTraceListener(Settings.Settings.Read().LogPath)
            {
                TraceOutputOptions = TraceOptions.Timestamp | TraceOptions.ThreadId | TraceOptions.DateTime |
                                     TraceOptions.ProcessId
            };

            Trace.Listeners.Add(listener);
            Trace.WriteLine("\n\n\n*******************************************\nStarting application");

#if TESTS
            RunTests();
#else
            StartProgram();
#endif
        }

        private void WriteEx(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Trace.WriteLine($"UNHANDLED\n\n{e.Exception}");
        }

        private void StartProgram()
        {
            var window = new MainView();
            window.ShowDialog();

            Current?.Shutdown(0);
        }

        private void RunTests()
        {
            //var notifier = new Notifier(cfg =>
            //{
            //    cfg.PositionProvider = new PrimaryScreenPositionProvider(Corner.BottomRight, 10, 10);

            //    cfg.LifetimeSupervisor = new TimeAndCountBasedLifetimeSupervisor(TimeSpan.FromMinutes(1),
            //        MaximumNotificationCount.FromCount(10));
            //});

            //Task.Run(async () =>
            //{
            //    await Task.Delay(TimeSpan.FromSeconds(5));

            //    try
            //    {
            //        notifier.ShowError("ОШИИИБКАААААА");
            //    }
            //    catch (Exception e)
            //    {
            //        Trace.WriteLine(e);
            //    }
                
            //});

            //var w1 = new Window
            //{
            //    VerticalContentAlignment = VerticalAlignment.Top,
            //    Content = new FilterViewModel(TfsAPI.Constants.WorkItemTypes.Task, TfsAPI.Constants.WorkItemTypes.Pbi)
            //};

            //w1.ShowDialog();

            //CultureInfo ci = CultureInfo.InstalledUICulture;

            //Thread.CurrentThread.CurrentUICulture = ci;
            //Thread.CurrentThread.CurrentCulture = ci;

            //var w = new Window
            //{
            //    Content = new Calendar()
            //};

            //w.Language = XmlLanguage.GetLanguage(ci.IetfLanguageTag);

            //w.ShowDialog();

            var id = 80439;
            var api = new TfsApi("https://msk-tfs1.securitycode.ru/tfs/Endpoint Security");

            var item = api.FindById(id);
            var assigned = new ItemsAssignedBaloonViewModel(new[] {item}, "Новый элемент был назначен");

            WindowManager.ShowBaloon(assigned);


            var write = new WriteOffBaloonViewModel(new ScheduleWorkArgs(item, 4));
            WindowManager.ShowBaloon(write);

            var items = api.GetMyWorkItems();

            //var response = new NewResponsesBaloonViewModel(items.Where(x => x.IsTypeOf(WorkItemTypes.ReviewResponse)),
            //    items.Where(x => x.IsTypeOf(WorkItemTypes.CodeReview)), api, "Мои проверки кода");

            //WindowManager.ShowBaloon(response);


            //var vm = new SettingsViewModel("", null);
            //WindowManager.ShowDialog(vm, "Настройки", 450, 500);

            //WindowManager.ShowBaloon(new WriteOffBaloonViewModel("Таймер списания времени"));

            //WindowManager.ShowDialog(new TestDialogViewModel(true, true), "Wait for error", width: 300, height: 200);
            //WindowManager.ShowDialog(new TestDialogViewModel(false, false), "No error no awaiting", width: 300, height: 200);
        }
    }
}