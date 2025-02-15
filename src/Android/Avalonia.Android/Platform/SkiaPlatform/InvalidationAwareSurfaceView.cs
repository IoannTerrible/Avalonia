using System;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Avalonia.Android.Platform.SkiaPlatform;
using Avalonia.Platform;

namespace Avalonia.Android
{
    internal abstract class InvalidationAwareSurfaceView : SurfaceView, ISurfaceHolderCallback, INativePlatformHandleSurface
    {
        bool _invalidateQueued;
        private bool _isDisposed;
        private bool _isSurfaceValid;
        readonly object _lock = new object();
        private readonly Handler _handler;

        internal event EventHandler? SurfaceWindowCreated;

        IntPtr IPlatformHandle.Handle => _isSurfaceValid && Holder?.Surface?.Handle is { } handle ?
            AndroidFramebuffer.ANativeWindow_fromSurface(JNIEnv.Handle, handle) :
            default;

        public InvalidationAwareSurfaceView(Context context) : base(context)
        {
            if (Holder is null)
                throw new InvalidOperationException(
                    "SurfaceView.Holder was not expected to be null during InvalidationAwareSurfaceView initialization.");

            Holder.AddCallback(this);
            Holder.SetFormat(global::Android.Graphics.Format.Transparent);
            _handler = new Handler(context.MainLooper!);
        }

        public override void Invalidate()
        {
            lock (_lock)
            {
                if (_invalidateQueued)
                    return;
                _handler.Post(() =>
                {
                    if (_isDisposed || Holder?.Surface?.IsValid != true)
                        return;
                    try
                    {
                        DoDraw();
                    }
                    catch (Exception e)
                    {
                        Log.WriteLine(LogPriority.Error, "Avalonia", e.ToString());
                    }
                });
            }
        }

        internal new void Dispose()
        {
            _isDisposed = true;
        }

        public void SurfaceChanged(ISurfaceHolder holder, Format format, int width, int height)
        {
            _isSurfaceValid = true;
            Log.Info("AVALONIA", "Surface Changed");
            DoDraw();
        }

        public void SurfaceCreated(ISurfaceHolder holder)
        {
            _isSurfaceValid = true;
            Log.Info("AVALONIA", "Surface Created");
            SurfaceWindowCreated?.Invoke(this, EventArgs.Empty);
            DoDraw();
        }

        public void SurfaceDestroyed(ISurfaceHolder holder)
        {
            _isSurfaceValid = false;
            Log.Info("AVALONIA", "Surface Destroyed");

        }

        protected void DoDraw()
        {
            lock (_lock)
            {
                _invalidateQueued = false;
            }
            Draw();
        }
        protected abstract void Draw();
        public string HandleDescriptor => "SurfaceView";

        public PixelSize Size => new(Holder?.SurfaceFrame?.Width() ?? 1, Holder?.SurfaceFrame?.Height() ?? 1);

        public double Scaling => Resources?.DisplayMetrics?.Density ?? 1;
    }
}
