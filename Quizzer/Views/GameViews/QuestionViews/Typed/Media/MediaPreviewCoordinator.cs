using Quizzer.DataModels.Enumerations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace Quizzer.Views.GameViews.QuestionViews.Typed.Media
{
    public static class MediaPreviewCoordinator
    {
        private static readonly object SyncRoot = new();

        private static readonly List<Window> RegisteredOwners = new();
        private static readonly List<MediaPreviewWindow> ActiveWindows = new();

        private static bool isClosingGroup;
        private static Guid activeGroupId = Guid.Empty;

        public static void SetOwnerWindows(IEnumerable<Window> windows)
        {
            lock (SyncRoot)
            {
                RegisteredOwners.Clear();

                RegisteredOwners.AddRange(
                    windows
                        .Where(w => w != null)
                        .Distinct());
            }
        }

        public static void ClearOwnerWindows()
        {
            lock (SyncRoot)
            {
                RegisteredOwners.Clear();
            }
        }

        public static IReadOnlyList<Window> GetOwnerWindows()
        {
            lock (SyncRoot)
            {
                return RegisteredOwners.ToList();
            }
        }

        public static void Show(string filePath, ResourceType resourceType, int count = int.MaxValue)
        {
            lock (SyncRoot)
            {
                ShowInternal(RegisteredOwners, filePath, resourceType, count);
            }
        }

        public static void ShowOnAll(string filePath, ResourceType resourceType)
        {
            lock (SyncRoot)
            {
                ShowInternal(RegisteredOwners, filePath, resourceType, RegisteredOwners.Count);
            }
        }

        public static void ShowOnFirst(string filePath, ResourceType resourceType)
        {
            lock (SyncRoot)
            {
                ShowInternal(RegisteredOwners, filePath, resourceType, 1);
            }
        }

        public static void StartAll()
        {
            lock (SyncRoot)
            {
                foreach (var window in ActiveWindows.ToList())
                    window.StartPlayback();
            }
        }

        public static void StopAll()
        {
            lock (SyncRoot)
            {
                foreach (var window in ActiveWindows.ToList())
                    window.StopPlayback();
            }
        }

        public static void CloseAll()
        {
            lock (SyncRoot)
            {
                CloseAllInternal();
            }
        }

        private static void ShowInternal(
            IEnumerable<Window> owners,
            string filePath,
            ResourceType resourceType,
            int count)
        {
            if (resourceType != ResourceType.Image && resourceType != ResourceType.Video)
                return;

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return;

            var ownerList = owners
                .Where(w => w != null)
                .Distinct()
                .Take(Math.Max(1, count))
                .ToList();

            if (ownerList.Count == 0)
                return;

            CloseAllInternal();

            activeGroupId = Guid.NewGuid();

            for (int i = 0; i < ownerList.Count; i++)
            {
                var owner = ownerList[i];
                bool isControlWindow = i == 0;

                var preview = new MediaPreviewWindow(
                    activeGroupId,
                    filePath,
                    resourceType,
                    isControlWindow);

                preview.Owner = owner;
                preview.WindowStartupLocation = WindowStartupLocation.Manual;
                preview.WindowState = WindowState.Normal;
                preview.SizeToContent = SizeToContent.Manual;

                Rect bounds = ScreenHelper.GetMonitorBoundsDip(owner, useWorkArea: false);

                preview.Left = bounds.Left;
                preview.Top = bounds.Top;
                preview.Width = bounds.Width;
                preview.Height = bounds.Height;

                preview.Closed += Preview_Closed;

                ActiveWindows.Add(preview);
            }

            foreach (var preview in ActiveWindows.ToList())
                preview.Show();
        }

        private static void Preview_Closed(object? sender, EventArgs e)
        {
            lock (SyncRoot)
            {
                if (isClosingGroup)
                    return;

                CloseAllInternal();
            }
        }

        private static void CloseAllInternal()
        {
            isClosingGroup = true;

            try
            {
                foreach (var window in ActiveWindows.ToList())
                {
                    window.Closed -= Preview_Closed;

                    if (window.IsLoaded)
                        window.Close();
                }

                ActiveWindows.Clear();
                activeGroupId = Guid.Empty;
            }
            finally
            {
                isClosingGroup = false;
            }
        }
    }
}