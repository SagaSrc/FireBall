#region Fireball License
//    Copyright (C) 2005  Sebastian Faltoni sebastian{at}dotnetfireball{dot}net
//
//    This library is free software; you can redistribute it and/or
//    modify it under the terms of the GNU Lesser General Public
//    License as published by the Free Software Foundation; either
//    version 2.1 of the License, or (at your option) any later version.
//
//    This library is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
//    Lesser General Public License for more details.
//
//    You should have received a copy of the GNU Lesser General Public
//    License along with this library; if not, write to the Free Software
//    Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA

#endregion
#region Original License
// *****************************************************************************
// 
//  Copyright 2004, Weifen Luo
//  All rights reserved. The software and associated documentation 
//  supplied hereunder are the proprietary information of Weifen Luo
//  and are supplied subject to licence terms.
// 
//  WinFormsUI Library Version 1.0
// *****************************************************************************
#endregion

using System;
using System.Windows.Forms;
using System.Drawing;
using System.Runtime.InteropServices;

namespace Fireball.Docking
{
	internal class AutoHideWindow : Panel
	{
		#region consts
		private const int ANIMATE_TIME = 100;	// in mini-seconds
		private const string _ExceptionInvalidActiveContent = "DockPanel.ActiveAutoHideContent.InvalidValue";
		#endregion

		private Timer m_timerMouseTrack;
		private AutoHideWindowSplitter m_splitter;

		public AutoHideWindow(DockContainer dockPanel)
		{
			m_dockPanel = dockPanel;

			m_timerMouseTrack = new Timer();
			m_timerMouseTrack.Tick += new EventHandler(TimerMouseTrack_Tick);

			Visible = false;
			m_splitter = new AutoHideWindowSplitter();
			Controls.Add(m_splitter);
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				m_timerMouseTrack.Dispose();
			}
			base.Dispose(disposing);
		}

		private DockContainer m_dockPanel = null;
		public DockContainer DockPanel
		{
			get	{	return m_dockPanel;	}
		}

		private DockPane m_activePane = null;
		public DockPane ActivePane
		{
			get	{	return m_activePane;	}
		}
		private void SetActivePane()
		{
			DockPane value = (ActiveContent == null ? null : ActiveContent.DockHandler.Pane);

			if (value == m_activePane)
				return;

			if (m_activePane != null)
				if (m_activePane.IsActivated)
					DockPanel.Focus();

			m_activePane = value;
		}

		private IDockableWindow m_activeContent = null;
		public IDockableWindow ActiveContent
		{
			get	{	return m_activeContent;	}
			set
			{
				if (value == m_activeContent)
					return;

				if (value != null)
				{
					if (!DockHelper.IsDockStateAutoHide(value.DockHandler.DockState) || value.DockHandler.DockPanel != DockPanel)
						throw(new InvalidOperationException(ResourceHelper.GetString(_ExceptionInvalidActiveContent)));
				}

				DockPanel.SuspendLayout();

				if (m_activeContent != null)
					AnimateWindow(false);

				m_activeContent = value;
				SetActivePane();
				if (ActivePane != null)
					ActivePane.ActiveContent = m_activeContent;

				if (m_activeContent != null)
					AnimateWindow(true);

				DockPanel.ResumeLayout();
				DockPanel.RefreshAutoHideStrip();

				SetTimerMouseTrack();
			}
		}

		public DockState DockState
		{
			get	{	return ActiveContent == null ? DockState.Unknown : ActiveContent.DockHandler.DockState;	}
		}

		private bool m_flagAnimate = true;
		private bool FlagAnimate
		{
			get	{	return m_flagAnimate;	}
			set	{	m_flagAnimate = value;	}
		}

		private bool m_flagDragging = false;
		internal bool FlagDragging
		{
			get	{	return m_flagDragging;	}
			set
			{
				if (m_flagDragging == value)
					return;

				m_flagDragging = value;
				SetTimerMouseTrack();
			}
		}

		private void AnimateWindow(bool show)
		{
			if (!FlagAnimate && Visible != show)
			{
				Visible = show;
				return;
			}

			Parent.SuspendLayout();

			Rectangle rectSource = GetBounds(!show);
			Rectangle rectTarget = GetBounds(show);
			int dxLoc, dyLoc;
			int dWidth, dHeight;
			dxLoc = dyLoc = dWidth = dHeight = 0;
			if (DockState == DockState.DockTopAutoHide)
				dHeight = show ? 1 : -1;
			else if (DockState == DockState.DockLeftAutoHide)
				dWidth = show ? 1 : -1;
			else if (DockState == DockState.DockRightAutoHide)
			{
				dxLoc = show ? -1 : 1;
				dWidth = show ? 1 : -1;
			}
			else if (DockState == DockState.DockBottomAutoHide)
			{
				dyLoc = (show ? -1 : 1);
				dHeight = (show ? 1 : -1);
			}

			if (show)
			{
				Bounds = new Rectangle(-rectTarget.Width, -rectTarget.Height, rectTarget.Width, rectTarget.Height);
				if (Visible == false)
					Visible = true;
				PerformLayout();
			}

			SuspendLayout();

			LayoutAnimateWindow(rectSource);
			if (Visible == false)
				Visible = true;

			int speedFactor = 1;
			int totalPixels = (rectSource.Width != rectTarget.Width) ?
				Math.Abs(rectSource.Width - rectTarget.Width) :
				Math.Abs(rectSource.Height - rectTarget.Height);
			int remainPixels = totalPixels;
			DateTime startingTime = DateTime.Now;
			while (rectSource != rectTarget)
			{
				DateTime startPerMove = DateTime.Now;

				rectSource.X += dxLoc * speedFactor;
				rectSource.Y += dyLoc * speedFactor;
				rectSource.Width += dWidth * speedFactor;
				rectSource.Height += dHeight * speedFactor;
				if (Math.Sign(rectTarget.X - rectSource.X) != Math.Sign(dxLoc))
					rectSource.X = rectTarget.X;
				if (Math.Sign(rectTarget.Y - rectSource.Y) != Math.Sign(dyLoc))
					rectSource.Y = rectTarget.Y;
				if (Math.Sign(rectTarget.Width - rectSource.Width) != Math.Sign(dWidth))
					rectSource.Width = rectTarget.Width;
				if (Math.Sign(rectTarget.Height - rectSource.Height) != Math.Sign(dHeight))
					rectSource.Height = rectTarget.Height;
				
				LayoutAnimateWindow(rectSource);
				if (Parent != null)
					Parent.Update();

				remainPixels -= speedFactor;

				while (true)
				{
					TimeSpan time = new TimeSpan(0, 0, 0, 0, ANIMATE_TIME);
					TimeSpan elapsedPerMove = DateTime.Now - startPerMove;
					TimeSpan elapsedTime = DateTime.Now - startingTime;
					if (((int)((time - elapsedTime).TotalMilliseconds)) <= 0)
					{
						speedFactor = remainPixels;
						break;
					}
					else
						speedFactor = remainPixels * (int)elapsedPerMove.TotalMilliseconds / (int)((time - elapsedTime).TotalMilliseconds);
					if (speedFactor >= 1)
						break;
				}
			}
			ResumeLayout();
			Parent.ResumeLayout();
		}

		private void LayoutAnimateWindow(Rectangle rect)
		{
			Bounds = rect;

			Rectangle rectClient = ClientRectangle;

			if (DockState == DockState.DockLeftAutoHide)
				ActivePane.Location = new Point(rectClient.Right - 2 - Measures.SplitterSize - ActivePane.Width, ActivePane.Location.Y);
			else if (DockState == DockState.DockTopAutoHide)
				ActivePane.Location = new Point(ActivePane.Location.X, rectClient.Bottom - 2 - Measures.SplitterSize - ActivePane.Height);
		}

		private Rectangle GetBounds(bool show)
		{
			if (DockState == DockState.Unknown)
				return Rectangle.Empty;

			Rectangle rect = DockPanel.AutoHideWindowBounds;

			if (show)
				return rect;

			if (DockState == DockState.DockLeftAutoHide)
				rect.Width = 0;
			else if (DockState == DockState.DockRightAutoHide)
			{
				rect.X += rect.Width;
				rect.Width = 0;
			}
			else if (DockState == DockState.DockTopAutoHide)
				rect.Height = 0;
			else
			{
				rect.Y += rect.Height;
				rect.Height = 0;
			}

			return rect;
		}

		private void SetTimerMouseTrack()
		{
			if (ActivePane == null || ActivePane.IsActivated || FlagDragging)
			{
				m_timerMouseTrack.Enabled = false;
				return;
			}

			// start the timer
			uint hovertime = 0;

			User32.SystemParametersInfo(Win32.SystemParametersInfoActions.GetMouseHoverTime, 0, ref hovertime, 0);

			// assign a default value 400 in case of setting Timer.Interval invalid value exception
			if (((int)hovertime) <= 0)
				hovertime = 400;

			m_timerMouseTrack.Interval = 2 * (int)hovertime;
			m_timerMouseTrack.Enabled = true;
		}

		protected virtual Rectangle DisplayingRectangle
		{
			get
			{
				Rectangle rect = ClientRectangle;

				// exclude the border and the splitter
				if (DockState == DockState.DockBottomAutoHide)
				{
					rect.Y += 2 + Measures.SplitterSize;
					rect.Height -= 2 + Measures.SplitterSize;
				}
				else if (DockState == DockState.DockRightAutoHide)
				{
					rect.X += 2 + Measures.SplitterSize;
					rect.Width -= 2 + Measures.SplitterSize;
				}
				else if (DockState == DockState.DockTopAutoHide)
					rect.Height -= 2 + Measures.SplitterSize;
				else if (DockState == DockState.DockLeftAutoHide)
					rect.Width -= 2 + Measures.SplitterSize;

				return rect;
			}
		}

		protected override void OnLayout(LayoutEventArgs levent)
		{
			DockPadding.All = 0;
			if (DockState == DockState.DockLeftAutoHide)
			{
				DockPadding.Right = 2;
				m_splitter.Dock = DockStyle.Right;
			}
			else if (DockState == DockState.DockRightAutoHide)
			{
				DockPadding.Left = 2;
				m_splitter.Dock = DockStyle.Left;
			}
			else if (DockState == DockState.DockTopAutoHide)
			{
				DockPadding.Bottom = 2;
				m_splitter.Dock = DockStyle.Bottom;
			}
			else if (DockState == DockState.DockBottomAutoHide)
			{
				DockPadding.Top = 2;
				m_splitter.Dock = DockStyle.Top;
			}

			foreach (Control c in Controls)
			{
				DockPane pane = c as DockPane;
				if (pane == null)
					continue;

				if (pane == ActivePane)
					pane.Bounds = DisplayingRectangle;
				else
					pane.Bounds = Rectangle.Empty;
			}

			base.OnLayout(levent);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			// Draw the border
			Graphics g = e.Graphics;

			if (DockState == DockState.DockBottomAutoHide)
				g.DrawLine(SystemPens.ControlLightLight, 0, 1, ClientRectangle.Right, 1);
			else if (DockState == DockState.DockRightAutoHide)
				g.DrawLine(SystemPens.ControlLightLight, 1, 0, 1, ClientRectangle.Bottom);
			else if (DockState == DockState.DockTopAutoHide)
			{
				g.DrawLine(SystemPens.ControlDark, 0, ClientRectangle.Height - 2, ClientRectangle.Right, ClientRectangle.Height - 2);
				g.DrawLine(SystemPens.ControlDarkDark, 0, ClientRectangle.Height - 1, ClientRectangle.Right, ClientRectangle.Height - 1);
			}
			else if (DockState == DockState.DockLeftAutoHide)
			{
				g.DrawLine(SystemPens.ControlDark, ClientRectangle.Width - 2, 0, ClientRectangle.Width - 2, ClientRectangle.Bottom);
				g.DrawLine(SystemPens.ControlDarkDark, ClientRectangle.Width - 1, 0, ClientRectangle.Width - 1, ClientRectangle.Bottom);
			}

			base.OnPaint (e);
		}

		public void RefreshActiveContent()
		{
			if (ActiveContent == null)
				return;

			if (!DockHelper.IsDockStateAutoHide(ActiveContent.DockHandler.DockState))
			{
				FlagAnimate = false;
				ActiveContent = null;
				FlagAnimate = true;
			}
		}

		public void RefreshActivePane()
		{
			SetTimerMouseTrack();
		}

		private void TimerMouseTrack_Tick(object sender, EventArgs e)
		{
			if (ActivePane == null || ActivePane.IsActivated)
			{
				m_timerMouseTrack.Enabled = false;
				return;
			}

			DockPane pane = ActivePane;
			Point ptMouseInAutoHideWindow = PointToClient(Control.MousePosition);
			Point ptMouseInDockPanel = DockPanel.PointToClient(Control.MousePosition);

			Rectangle rectTabStrip = DockPanel.GetTabStripRectangle(pane.DockState);

			if (!ClientRectangle.Contains(ptMouseInAutoHideWindow) && !rectTabStrip.Contains(ptMouseInDockPanel))
			{
				ActiveContent = null;
				m_timerMouseTrack.Enabled = false;
			}
		}

		protected override void WndProc(ref Message m)
		{
			if (m.Msg == (int)Win32.Msgs.WM_WINDOWPOSCHANGING)
			{
				int offset = (int)Marshal.OffsetOf(typeof(Win32.WINDOWPOS), "flags");
				int flags = Marshal.ReadInt32(m.LParam, offset);
				Marshal.WriteInt32(m.LParam, offset, flags | (int)Win32.FlagsSetWindowPos.SWP_NOCOPYBITS);
			}

			base.WndProc (ref m);
		}

	}
}
