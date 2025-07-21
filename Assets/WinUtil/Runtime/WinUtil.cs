#if UNITASK_SUPPORT
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace work.ctrl3d
{
    public static class WinUtil
    {
        public static async UniTask<Process> ProcessStartAsync(string filePath, string arguments = "",
            ProcessWindowStyle windowStyle = ProcessWindowStyle.Normal, bool useShellExecute = false,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var process = new Process();
                process.StartInfo.FileName = filePath;
                process.StartInfo.Arguments = arguments;
                process.StartInfo.WindowStyle = windowStyle;
                process.StartInfo.UseShellExecute = useShellExecute;
                process.Start();

                await UniTask.WaitUntil(() => !string.IsNullOrEmpty(process.ProcessName),
                    cancellationToken: cancellationToken);
                return process;
            }
            catch (OperationCanceledException)
            {
                Debug.LogError($"Canceled: Process '{filePath}' was canceled.");
                return null;
            }
            catch (Win32Exception ex)
            {
                Debug.LogError($"Win32Error: {ex.Message} (Error Code: {ex.NativeErrorCode}) - File: {filePath}");
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Unexpected Error: {ex.Message} - File: {filePath}");
                return null;
            }
        }

        /// <summary>
        /// 창의 위치를 변경하고 크기를 조정합니다.
        /// </summary>
        /// <param name="hWnd">창의 핸들입니다.</param>
        /// <param name="x">창의 왼쪽 가장자리의 새 위치입니다.</param>
        /// <param name="y">창의 상단의 새 위치입니다.</param>
        /// <param name="width">창의 새 너비입니다.</param>
        /// <param name="height">창의 새 높이입니다.</param>
        public static void MoveAndResizeWindow(IntPtr hWnd, int x, int y, int width, int height)
        {
            if (hWnd == IntPtr.Zero)
            {
                Debug.LogError("활성 창 핸들을 가져오는 데 실패했습니다.");
                return;
            }

            if (!WinAPI.SetWindowPos(hWnd, IntPtr.Zero, x, y, width, height, SWP.NOZORDER))
            {
                Debug.LogError("창을 이동하고 크기를 조정하는 데 실패했습니다.");
            }
        }

        /// <summary>
        /// 주어진 창 이름에 대한 핸들을 가져옵니다.
        /// </summary>
        /// <param name="windowName"></param>
        /// <returns></returns>
        public static IntPtr GetWindowHandle(string windowName)
        {
            var hWnd = WinAPI.FindWindow(null, windowName);
            return hWnd.Equals(IntPtr.Zero) ? IntPtr.Zero : hWnd;
        }

        /// <summary>
        /// 주어진 창 이름에 대한 핸들을 비동기로 가져옵니다.
        /// </summary>
        /// <param name="windowName"></param>
        /// <param name="timeoutMilliseconds"></param>
        /// <returns></returns>
        public static async UniTask<IntPtr> GetWindowHandleAsync(string windowName, int timeoutMilliseconds = 1000)
        {
            var hWnd = IntPtr.Zero;
            var cts = new CancellationTokenSource(timeoutMilliseconds);

            try
            {
                await UniTask.WaitUntil(() =>
                {
                    hWnd = WinAPI.FindWindow(null, windowName);
                    return hWnd != IntPtr.Zero;
                }, cancellationToken: cts.Token);

                return hWnd;
            }
            catch (OperationCanceledException)
            {
                return IntPtr.Zero;
            }
        }

        public static async UniTask<string> GetProcessWindowTitleAsync(string processName)
        {
            var hWnd = await GetWindowHandleByProcessNameAsync(processName);
            return hWnd == IntPtr.Zero ? null : GetWindowTitle(hWnd);
        }

        public static IntPtr GetUnityHandle() => GetWindowHandle(Application.productName);

        public static string GetWindowTitle(IntPtr hWnd)
        {
            var length = WinAPI.GetWindowTextLength(hWnd);
            if (length == 0) return null;

            var builder = new StringBuilder(length + 1);
            WinAPI.GetWindowText(hWnd, builder, builder.Capacity);
            return builder.ToString();
        }

        public static IntPtr[] GetWindowHandlesByProcessName(string processName)
        {
            var processes = Process.GetProcessesByName(processName);
            var handles = new IntPtr[processes.Length];

            for (var i = 0; i < processes.Length; i++)
            {
                handles[i] = processes[i].MainWindowHandle;
            }

            return handles;
        }

        public static async UniTask<IntPtr[]> GetWindowHandlesByProcessNameAsync(string processName,
            int timeoutMilliseconds = 1000)
        {
            // 부분 문자열 일치를 위한 검색
            var processes = Process.GetProcesses()
                .Where(p => p.ProcessName.StartsWith(processName, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            var handles = new List<IntPtr>();

            var cts = new CancellationTokenSource(timeoutMilliseconds);

            try
            {
                await UniTask.WhenAll(processes.Select(async process =>
                {
                    if (cts.IsCancellationRequested) return;

                    while (process.MainWindowHandle == IntPtr.Zero && !cts.IsCancellationRequested)
                    {
                        await UniTask.Delay(10, cancellationToken: cts.Token);
                    }

                    if (process.MainWindowHandle != IntPtr.Zero)
                    {
                        lock (handles)
                        {
                            handles.Add(process.MainWindowHandle);
                        }
                    }
                }));
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"타임아웃: {processName} 프로세스의 핸들을 찾을 수 없습니다.");
                return Array.Empty<IntPtr>();
            }

            return handles.ToArray();
        }

        public static async UniTask<IntPtr> GetWindowHandleByProcessNameAsync(string processName,
            int timeoutMilliseconds = 1000)
        {
            var handles = await GetWindowHandlesByProcessNameAsync(processName, timeoutMilliseconds);
            return handles.Length > 0 ? handles[0] : IntPtr.Zero;
        }

        public static IntPtr GetWindowHandleByProcessId(int processId)
        {
            var process = Process.GetProcessById(processId);
            return process.MainWindowHandle;
        }

        public static IntPtr[] GetWindowHandlesByProcessId(int processId)
        {
            var process = Process.GetProcessById(processId);
            var handles = new IntPtr[1];
            handles[0] = process.MainWindowHandle;
            return handles;
        }

        /// <summary>
        /// 주어진 이름의 윈도우에 포커스를 설정합니다.
        /// </summary>
        /// <param name="hWnd">창의 핸들입니다.</param>
        public static void SetFocus(IntPtr hWnd = default)
        {
            var handle = hWnd == IntPtr.Zero ? GetUnityHandle() : hWnd;
            WinAPI.SetForegroundWindow(handle);
        }

        public static void Show(IntPtr hWnd) => WinAPI.ShowWindow(hWnd, SW.SHOW);
        public static void Hide(IntPtr hWnd) => WinAPI.ShowWindow(hWnd, SW.HIDE);
        public static void SendKeyDown(byte key) => WinAPI.keybd_event(key, 0, 0, 0);
        public static void SendKeyUp(byte key) => WinAPI.keybd_event(key, 0, VK.KEYEVENTF_KEYUP, 0);

        public static async UniTask SendKeyAsync(IntPtr hWnd, byte key)
        {
            SetFocus(hWnd);

            SendKeyDown(key);
            await UniTask.Delay(TimeSpan.FromMilliseconds(100));
            SendKeyUp(key);
        }

        /// <summary>
        /// Z 순서의 맨 아래에 있는 창을 Places. hWnd 매개 변수가 맨 위 창을 식별하는 경우 창의 맨 위 상태 손실되고 다른 모든 창의 맨 아래에 배치됩니다.
        /// </summary>
        /// <param name="hWnd"></param>
        public static void Bottom(IntPtr hWnd = default)
        {
            var handle = hWnd == IntPtr.Zero ? GetUnityHandle() : hWnd;
            WinAPI.SetWindowPos(handle, HWND.BOTTOM, 0, 0, 0, 0, SWP.NOMOVE | SWP.NOSIZE);
        }

        /// <summary>
        /// 맨 위가 아닌 모든 창 위에 창을 Places. 즉, 맨 위 창 뒤에 있습니다. 창이 이미 맨 위가 아닌 창인 경우에는 이 플래그가 적용되지 않습니다.
        /// </summary>
        /// <param name="hWnd"></param>
        public static void NoTopMost(IntPtr hWnd = default)
        {
            var handle = hWnd == IntPtr.Zero ? GetUnityHandle() : hWnd;
            WinAPI.SetWindowPos(handle, HWND.NOTOPMOST, 0, 0, 0, 0, SWP.NOMOVE | SWP.NOSIZE);
        }

        /// <summary>
        /// Z 순서의 맨 위에 있는 창을 Places.
        /// </summary>
        /// <param name="hWnd"></param>
        public static void Top(IntPtr hWnd = default)
        {
            var handle = hWnd == IntPtr.Zero ? GetUnityHandle() : hWnd;
            WinAPI.SetWindowPos(handle, HWND.TOP, 0, 0, 0, 0, SWP.NOMOVE | SWP.NOSIZE);
        }

        /// <summary>
        /// 맨 위가 아닌 모든 창 위에 창을 Places. 창은 비활성화된 경우에도 맨 위 위치를 유지합니다.
        /// </summary>
        /// <param name="hWnd"></param>
        public static void TopMost(IntPtr hWnd = default)
        {
            var handle = hWnd == IntPtr.Zero ? GetUnityHandle() : hWnd;
            WinAPI.SetWindowPos(handle, HWND.TOPMOST, 0, 0, 0, 0, SWP.NOMOVE | SWP.NOSIZE);
        }


        public static void SetZOrder(string zOrder)
        {
            switch (zOrder.ToUpper())
            {
                case "BOTTOM":
                    Bottom();
                    break;

                case "NOTOPMOST":
                    NoTopMost();
                    break;

                case "TOPMOST":
                    TopMost();
                    break;

                case "TOP":
                    Top();
                    break;
            }
        }


        /// <summary>
        /// 현재 실행 중인 윈도우를 종료합니다.
        /// </summary>
        /// <param name="hWnd">창의 핸들입니다.</param>
        public static bool CloseWindow(IntPtr hWnd)
        {
            if (hWnd.Equals(IntPtr.Zero))
            {
                Debug.LogError($"[{hWnd}] 찾을 수 없음.");
                return false;
            }

            WinAPI.SendMessage(hWnd, WM.CLOSE, IntPtr.Zero, IntPtr.Zero);
            return true;
        }

        /// <summary>
        /// 창을 팝업 창으로 설정합니다.
        /// </summary>
        /// <param name="hWnd"></param>
        /// <returns></returns>
        public static uint[] SetPopupWindow(IntPtr hWnd)
        {
            var style = GetWindowStyle(hWnd);
            var exStyle = GetWindowExStyle(hWnd);

            var originalStyle = style;
            var originalExStyle = exStyle;

            style = (style & ~(WS.BORDER | WS.CAPTION | WS.THICKFRAME)) | WS.POPUP;
            exStyle &= ~WS_EX.CLIENTEDGE;

            SetWindowStyle(hWnd, style);
            SetWindowExStyle(hWnd, exStyle);

            return new[] { originalStyle, originalExStyle };
        }

        public static void CancelPopupWindow(IntPtr hWnd)
        {
            var style = GetWindowStyle(hWnd);
            var exStyle = GetWindowExStyle(hWnd);

            style |= (WS.BORDER | WS.CAPTION | WS.THICKFRAME);
            style &= ~WS.POPUP;

            exStyle |= WS_EX.CLIENTEDGE;

            SetWindowStyle(hWnd, style);
            SetWindowExStyle(hWnd, exStyle);
        }

        public static uint GetWindowStyle(IntPtr hWnd) => WinAPI.GetWindowLong(hWnd, GWL.STYLE);
        public static uint GetWindowExStyle(IntPtr hWnd) => WinAPI.GetWindowLong(hWnd, GWL.EXSTYLE);

        public static void SetWindowStyle(IntPtr hWnd, uint style) =>
            WinAPI.SetWindowLong(hWnd, GWL.STYLE, style);

        public static void SetWindowExStyle(IntPtr hWnd, uint exStyle) =>
            WinAPI.SetWindowLong(hWnd, GWL.EXSTYLE, exStyle);
    }
}
#endif