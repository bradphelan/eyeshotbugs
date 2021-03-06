﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using devDept.Eyeshot;
using devDept.Eyeshot.Entities;
using devDept.Geometry;
using devDept.Graphics;
using ReactiveUI;
using Splat;
using Weingartner.EyeShot.Assembly3D.Weingartner.Utils;
using Brushes = System.Windows.Media.Brushes;
using MouseButton = System.Windows.Input.MouseButton;
using Rotation = devDept.Geometry.Rotation;
using Size = System.Drawing.Size;
using Point = System.Drawing.Point;
using Unit = System.Reactive.Unit;

namespace Weingartner.EyeShot.Assembly3D
{
    public class WgViewportLayout : Model
    {
        #region properties

        private Point _MouseLocation;
        private Point _StartPointScreen;
        private Point3D _Current;
        public double DisplayScalingFactor { get; set; } = 1.0;
        public bool IsOrtho { get; set; }
        private bool _IsMouseOutsideToolBar;

        public Plane CurrentDefinitionPlane
        {
            get
            {
                Camera.GetFrame(out _, out var xAxis, out var yAxis, out _);
                return new Plane(Point3D.Origin, xAxis, yAxis);
            }
        }

        private bool _CursorOutside;

        protected override void OnMouseLeave(EventArgs e)
        {
            _CursorOutside = true;
            base.OnMouseLeave(e);

            Invalidate();

        }

        public static readonly DependencyProperty IsDraftingModelProperty = DependencyProperty.Register
            (
             "IsDraftingModel"
             , typeof(bool)
             , typeof(WgViewportLayout)
             , new PropertyMetadata(false));

        public bool IsDraftingModel
        {
            get => (bool)GetValue(IsDraftingModelProperty);
            set => SetValue(IsDraftingModelProperty, value);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _CursorOutside = false;
            base.OnMouseEnter(e);
        }

        public static readonly DependencyProperty ShowEdgesObservableProperty = DependencyProperty.Register
            (
             "ShowEdgesObservable"
             , typeof(bool)
             , typeof(WgViewportLayout)
             , new PropertyMetadata(true));

        public bool ShowEdgesObservable
        {
            get => (bool)GetValue(ShowEdgesObservableProperty);
            set => SetValue(ShowEdgesObservableProperty, value);
        }

        public static readonly DependencyProperty ResetZoomObservableProperty = DependencyProperty.Register
            ("ResetZoomObservable"
            , typeof(IObservable<Unit>)
            , typeof(WgViewportLayout)
            , new PropertyMetadata(Observable.Never<Unit>())
            );

        public IObservable<Unit> ResetZoomObservable
        {
            get => (IObservable<Unit>)GetValue(ResetZoomObservableProperty);
            set => SetValue(ResetZoomObservableProperty, value ?? Observable.Never<Unit>());
        }

        public static readonly DependencyProperty Assembly3DProperty = DependencyProperty.Register
            ("Assembly3D"
             , typeof(Assembly3D)
             , typeof(WgViewportLayout)
             , new PropertyMetadata(default(Assembly3D))
             );

        public Assembly3D Assembly3D
        {
            get => (Assembly3D)GetValue(Assembly3DProperty);
            set => SetValue(Assembly3DProperty, value);
        }

        public static readonly DependencyProperty GridStepProperty = DependencyProperty.Register
            ("GridStep"
            , typeof(double)
            , typeof(WgViewportLayout)
            , new PropertyMetadata(new Grid().Step)
            );

        public double GridStep
        {
            get => (double)GetValue(GridStepProperty);
            set => SetValue(GridStepProperty, value);
        }

        public static readonly DependencyProperty GridBoundaryProperty = DependencyProperty.Register
            ("GridBoundary"
            , typeof(double)
            , typeof(WgViewportLayout)
            , new PropertyMetadata(0.1)
            );


        public double GridBoundary
        {
            get => (double)GetValue(GridBoundaryProperty);
            set => SetValue(GridBoundaryProperty, value);
        }

        public static readonly DependencyProperty ViewTypeProperty = DependencyProperty.Register
            ("ViewType"
            , typeof(viewType)
            , typeof(WgViewportLayout)
            , new PropertyMetadata(viewType.Top)
            );

        public viewType ViewType
        {
            get => (viewType)GetValue(ViewTypeProperty);
            set => SetValue(ViewTypeProperty, value);
        }

        public static readonly DependencyProperty GridMajorLineEveryProperty = DependencyProperty.Register
            ("GridMajorLineEvery"
            , typeof(int)
            , typeof(WgViewportLayout)
            , new PropertyMetadata(10)
            );

        public int GridMajorLineEvery
        {
            get => (int)GetValue(GridMajorLineEveryProperty);
            set => SetValue(GridMajorLineEveryProperty, value);
        }

        public static readonly DependencyProperty ProjectionTypeProperty = DependencyProperty.Register
            (
                "ProjectionType",
                typeof(projectionType),
                typeof(WgViewportLayout),
                new PropertyMetadata(default(projectionType)));

        public projectionType ProjectionType
        {
            get => (projectionType)GetValue(ProjectionTypeProperty);
            set => SetValue(ProjectionTypeProperty, value);
        }


        #endregion

        #region legend

        public static readonly DependencyProperty LegenMinValueProperty = DependencyProperty.Register
            ("LegenMinValue"
            , typeof(double)
            , typeof(WgViewportLayout)
            , new PropertyMetadata(0.0));

        public double LegenMinValue
        {
            get => (double)GetValue(LegenMinValueProperty);
            set => SetValue(LegenMinValueProperty, value);
        }

        public static readonly DependencyProperty LegendMaxValueProperty = DependencyProperty.Register
            ("LegendMaxValue"
            , typeof(double)
            , typeof(WgViewportLayout)
            , new PropertyMetadata(100.0));

        public double LegendMaxValue
        {
            get => (double)GetValue(LegendMaxValueProperty);
            set => SetValue(LegendMaxValueProperty, value);
        }

        public static readonly DependencyProperty LegendVisibleProperty = DependencyProperty.Register
            ("LegendVisible"
            , typeof(bool)
            , typeof(WgViewportLayout)
            , new PropertyMetadata(false));

        public bool LegendVisible
        {
            get => (bool)GetValue(LegendVisibleProperty);
            set => SetValue(LegendVisibleProperty, value);
        }

        #endregion

        public WgViewportLayout()
        {
            Utils.Unlock(this);
            GridStep = 0.1;
            GridMajorLineEvery = 10;
            ProjectionType = projectionType.Orthographic;
            ViewType = viewType.Top;

            //###########LineTypes#########################
            LineTypes.Add("Dash", new[] { 0.001f, -0.0005f });
            LineTypes.Add("DashDot", new[] { 0.001f, -0.0005f, 0.0f, -0.0005f });
            //#############################################


            AutoRefresh = true;

            Observable
                .FromEventPattern<MouseEventHandler, MouseEventArgs>
                (h => MouseMove += h
                , h => MouseMove -= h
                )
                .Sample(TimeSpan.FromSeconds(1.0 / 30))
                .ObserveOn(Dispatcher)
                .Subscribe(MouseMoveHandler);

            if (!DesignerProperties.GetIsInDesignMode(this))
            {
                this.LoadUnloadHandler(Init);
            }

            // Use this to debug solids with bad normals
            //this.Backface.ColorMethod = backfaceColorMethodType.Cull;

            //this.StartAnimation();

            // Clear the measurements when the viewport is unloaded
        }

        private IEnumerable<IDisposable> Init()
        {

            // Bind the assembly property to the viewport
            yield return EyeShot.Assembly3D.Assembly3D.BindAssemblyToViewport
                (this
                , p => p.Assembly3D
                , () =>
                {
                    //if (!AutoZoomOff)
                    //ZoomFit();
                });

            //#####This is necessary for getting the correct      ####
            //#####mouse position even if the Display is scaled!! ####
            DisplayScalingFactor = PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice.M11;
            //#################################################################
        }

        #region MouseHandling

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                SetView(viewType.Top);
                ZoomFit();
                Invalidate();
            }

            if (e.Key == Key.R && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                ActionMode = actionType.Rotate;
                ZoomFit();
                RotateCamera(Vector3D.AxisX, 90, true, true);
                ActionMode = actionType.None;
                Invalidate();
            }


            if (e.Key == Key.F12)
            {
                ClippingPlane1.Cancel();
                if (ClippingPlane1.Plane == Plane.XY)
                {
                    ClippingPlane1.Plane = Plane.YZ;
                }
                else if (ClippingPlane1.Plane == Plane.YZ)
                {
                    ClippingPlane1.Plane = Plane.ZX;
                }
                else if (ClippingPlane1.Plane == Plane.ZX)
                {
                    ClippingPlane1.Plane = Plane.XY;
                }
                else
                {
                    ClippingPlane1.Plane = Plane.YZ;
                }
                ClippingPlane1.Edit(Color.FromArgb(100, 0, 100, 100));
                Invalidate();
            }

            base.OnKeyDown(e);
        }

        protected void MouseMoveHandler(EventPattern<MouseEventArgs> eventPattern)
        {
            var e = eventPattern.EventArgs;

            var currentMousePos = Mouse.GetPosition(this);
            currentMousePos = new System.Windows.Point(currentMousePos.X * DisplayScalingFactor, currentMousePos.Y * DisplayScalingFactor);
            _MouseLocation = RenderContextUtility.ConvertPoint(currentMousePos);

            if (IsOrtho)
            {
                var xDiff = Math.Abs(_StartPointScreen.X - _MouseLocation.X);
                var yDiff = Math.Abs(_StartPointScreen.Y - _MouseLocation.Y);
                _MouseLocation = xDiff > yDiff
                    ? new Point(_MouseLocation.X, _StartPointScreen.Y)
                    : new Point(_StartPointScreen.X, _MouseLocation.Y);
            }


            // This code is required to draw the measurements correctly
            // Notice: PaintBackBuffer / SwapBuffers() are optimized --> they don't redraw the 
            // scene but a texture of the scene (that is captured internally after some camera movement or mouse click).
            // Invalidate() causes a full redraw of the scene, instead.

            // paint the viewport surface
            PaintBackBuffer();
            // consolidates the drawing
            SwapBuffers();
        }
        #endregion


        public static WgViewportLayout CreateViewportLayout()
        {
            var vpl = new WgViewportLayout();

            //activate Turbo mode https://devdept.zendesk.com/hc/en-us/articles/360015534994-Turbo-mode-former-FastZPR-
            //to avoid Block not found exceptions!!!!!!!
            vpl.Turbo.MaxComplexity = int.MaxValue;

            vpl.Viewports.Add(CreateViewport(vpl));
            vpl.AskForAntiAliasing = false;
            vpl.AntiAliasing = false;
            vpl.Rendered.SilhouettesDrawingMode = silhouettesDrawingType.Never;
            vpl.DisplayMode = displayType.Rendered;
            vpl.Rendered.ShadowMode = shadowType.Realistic;
            // This causes a bug 
            // See https://devdept.zendesk.com/hc/en-us/requests/10356?page=1
            vpl.Rendered.RealisticShadowQuality = realisticShadowQualityType.High;
            vpl.AmbientLight = Color.White;
            vpl.ButtonStyle.HighlightColor = Brushes.OrangeRed;
            vpl.VertexSize = 3;

            var lightVector1 = Vector3D.AxisY;
            const double angleInRadians = Math.PI / 8;
            lightVector1.TransformBy(new Rotation(-angleInRadians, Vector3D.AxisX));
            lightVector1.TransformBy(new Rotation(angleInRadians / 10, Vector3D.AxisZ));

            vpl.Light1.YieldShadow = true;
            vpl.Light1.Direction = lightVector1;
            vpl.Light1.Active = true;
            vpl.Light1.Color = Color.White;

            vpl.DefaultColor = Color.Blue;

            return vpl;
        }

        private static Viewport CreateViewport(WgViewportLayout vpl)
        {
            var backGroundTopColor = Brushes.WhiteSmoke;
            var vp = new Viewport
            {
                Background =
                         {
                             StyleMode = backgroundStyleType.Solid,
                             ColorTheme = colorThemeType.Auto,
                             TopColor = backGroundTopColor,
                         },
                CoordinateSystemIcon = new CoordinateSystemIcon { LabelColor = Brushes.White },
                ToolBars = new ObservableCollection<ToolBar>
                                    {
                                        new ToolBar
                                        {
                                            Position = ToolBar.positionType.HorizontalTopCenter
                                          , Height   = 10
                                        }
                                    },
                ViewCubeIcon = new ViewCubeIcon { HighlightColor = Brushes.OrangeRed },
            };

            vp.OriginSymbols.Add(new OriginSymbol { Visible = false });
            vp.Grids.Add(new Grid { Step = 10, AutoSize = true, AlwaysBehind = true, BorderColor = Brushes.Transparent });
            vp.ToolBars[0].Buttons.Add(new ZoomWindowToolBarButton());
            vp.ToolBars[0].Buttons.Add(new ZoomToolBarButton());
            vp.ToolBars[0].Buttons.Add(new PanToolBarButton());
            vp.ToolBars[0].Buttons.Add(new RotateToolBarButton());
            vp.ToolBars[0].Buttons.Add(new ZoomFitToolBarButton());
            vp.ToolBars[0].Buttons.Add(new MagnifyingGlassToolBarButton());

            //############## custom toolbar buttons #############################
            var assembly = Assembly.GetExecutingAssembly();
            var autoZoomButton = ToolBarButtons.CreateAutoZoomOffButton(vpl, assembly);
            vp.ToolBars[0].Buttons.Add(autoZoomButton);
            var clippingButton = ToolBarButtons.CreateClippingButton(vpl, assembly);
            vp.ToolBars[0].Buttons.Add(clippingButton);
            var shadingButton = ToolBarButtons.CreateShadingButton(vpl, assembly);
            vp.ToolBars[0].Buttons.Add(shadingButton);
            var wireFrameButton = ToolBarButtons.CreateWireFrameButton(vpl, assembly);
            vp.ToolBars[0].Buttons.Add(wireFrameButton);


            var separator = ToolBarButtons.CreateSeparator(assembly);
            vp.ToolBars[0].Buttons.Add(separator);

            var gridShowButton = ToolBarButtons.CreateGridButton(vpl, assembly);
            vp.ToolBars[0].Buttons.Add(gridShowButton);
            var markerButton = ToolBarButtons.CreateMarkerButton(vpl, assembly);
            vp.ToolBars[0].Buttons.Add(markerButton);
            var originSymbolButton = ToolBarButtons.CreateOriginSymbolButton(vpl, assembly);
            vp.ToolBars[0].Buttons.Add(originSymbolButton);

            //###################################################################
            //############## dev toolbar buttons #############################
            var saveButton = ToolBarButtons.CreateSaveButton(vpl, assembly);
            vp.ToolBars[0].Buttons.Add(saveButton);
            //###################################################################

            vpl.SetupPropertyHandlers(vp);

            return vp;
        }

        internal IDisposable SetupPropertyHandlers(Viewport viewport)
        {

            var c = new CompositeDisposable();

            this.WhenAnyValue(p => p.GridBoundary)
                .WhenViewportLayoutReady(this)
                .Subscribe(x =>
                {
                    viewport.Grid.Min = new Point2D(-x, -x);
                    viewport.Grid.Max = new Point2D(x, x);
                })
                .DisposeWith(c);

            this.WhenAnyValue(p => p.GridMajorLineEvery)
                .WhenViewportLayoutReady(this)
                .Subscribe(x => viewport.Grid.MajorLinesEvery = x)
                .DisposeWith(c);

            this.WhenAnyValue(p => p.ProjectionType)
                .WhenViewportLayoutReady(this)
                .Subscribe(x => Camera.ProjectionMode = x)
                .DisposeWith(c);

            this.WhenAnyValue(p => p.ViewType)
                .WhenViewportLayoutReady(this)
                .CombineLatest(this.Events().Loaded, (e, _) => e) // Every time it is loaded reset the view.
                .Subscribe(SetView)
                .DisposeWith(c);

            this.WhenAnyValue(p => p.GridStep)
                .WhenViewportLayoutReady(this)
                .Subscribe(x => viewport.Grid.Step = x)
                .DisposeWith(c);

            this.WhenAnyObservable(p => p.ResetZoomObservable)
                .WhenViewportLayoutReady(this)
                //.Throttle(TimeSpan.FromMilliseconds(100))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => ZoomFitAndInvalidate(!AutoZoomOff))
                .DisposeWith(c);

            this.WhenAnyValue(p => p.ShowEdgesObservable)
                .WhenViewportLayoutReady(this)
                .Subscribe(b => Rendered.ShowEdges = b)
                .DisposeWith(c);

            return c;
        }

        public static bool AutoZoomOff { get; set; }

        public void ZoomFitAndInvalidate(bool zoomFit)
        {
            try
            {
                if (zoomFit && Viewports.Count > 0)
                {
                    ZoomFit();
                }
                Invalidate();
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Exception: {e}");
            }
        }

        //protected override void OnResize( EventArgs e )
        //{
        //    //UpdateBoundingBox();
        //    //Clear();
        //    //Invalidate();
        //    //var foo = (Assembly3D?.AssemblyViewport == this);
        //    //var blocks = this.Blocks;
        //    //var entities = this.Entities;
        //    //var viewports = this.Viewports;
        //    base.OnResize( e );
        //}

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            //var previousSize = sizeInfo.PreviousSize;
            //var newSize = sizeInfo.NewSize;
            //this.ActiveViewport.CompileUserInterfaceElements();
            base.OnRenderSizeChanged(sizeInfo);
        }
    }
}
