﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Gui.Helper;
using Gui.Properties;
using Microsoft.TeamFoundation.Common;
using TfsAPI.TFS;

namespace Gui.ViewModels.DialogViewModels
{
    /// <summary>
    ///     Окошко с выбором подключения к TFS
    /// </summary>
    public class FirstConnectionViewModel : BindableExtended
    {
        public FirstConnectionViewModel()
        {
            using (var settings = Settings.Settings.Read())
            {
                RememberedConnections = settings.Connections?.ToList() ?? new List<string>();

                if (!RememberedConnections.IsNullOrEmpty())
                    Text = RememberedConnections.First();
            }

            CheckConnectionCommand = ObservableCommand.FromAsyncHandler(Connect, CanConnect);
            SubmitCommand = new ObservableCommand(() => { },
                () => Connection == ConnectionType.Success);
        }

        protected override string ValidateProperty(string prop)
        {
            if (prop == nameof(Text))
            {
                if (!Uri.TryCreate(Text, UriKind.Absolute, out var result))
                {
                    return Resources.AS_NotAWebAddress_Error;
                }

                if (Connection == ConnectionType.Failed)
                {
                    return Resources.AS_ConnectError;
                }
            }

            return base.ValidateProperty(prop);
        }

        protected override string ValidateOptionalProperty(string prop)
        {
            if (prop == nameof(Text))
            {
                if (Connection == ConnectionType.Success)
                {
                    return Resources.AS_ConnectionEstablished;
                }
            }

            return base.ValidateOptionalProperty(prop);
        }

        #region Fields

        private string _text;
        private IList<string> _rememberedConnections;
        private readonly ActionArbiterAsync _arbiter = new ActionArbiterAsync();
        private ConnectionType _connection;

        #endregion

        #region Properties

        public string Text
        {
            get => _text;
            set
            {
                if (SetProperty(ref _text, value)
                    && Connection == ConnectionType.Success)
                {
                    // Сбрасываем подключение
                    Connection = ConnectionType.Unknown;
                }
            }
        }

        public IList<string> RememberedConnections
        {
            get => _rememberedConnections;
            set => SetProperty(ref _rememberedConnections, value);
        }

        public ConnectionType Connection
        {
            get => _connection;
            set
            {
                if (SetProperty(ref _connection, value))
                {
                    // необходимо для валидации данных
                    OnPropertyChanged(nameof(Text));
                    //SubmitCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public ObservableCommand CheckConnectionCommand { get; }

        #endregion

        #region Command handlers

        public bool CanConnect()
        {
            return _arbiter.IsFree()
                   && !string.IsNullOrWhiteSpace(Text);
        }

        public async Task Connect()
        {
            var connected = await TfsApi.CheckConnection(Text);

            Connection = connected ? ConnectionType.Success : ConnectionType.Failed;

            CheckConnectionCommand.RaiseCanExecuteChanged();
            SubmitCommand.RaiseCanExecuteChanged();
        }

        #endregion
    }

    /// <summary>
    ///     Состояние соединения.
    /// </summary>
    public enum ConnectionType
    {
        /// <summary>
        /// Состояние неизвестно.
        /// </summary>
        Unknown,

        /// <summary>
        /// Соединение успешно установленно.
        /// </summary>
        Success,

        /// <summary>
        /// Во время соединения произошла ошибка.
        /// </summary>
        Failed
    }
}