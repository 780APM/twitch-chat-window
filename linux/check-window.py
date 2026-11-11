#!/usr/bin/env python3
"""Exercise the installed app under an isolated XWayland session."""
import ctypes as c
import os
import subprocess
import time
from pathlib import Path

x = c.CDLL('libX11.so.6')
shape = c.CDLL('libXext.so.6')
ptr, ulong, integer = c.c_void_p, c.c_ulong, c.c_int
x.XOpenDisplay.argtypes, x.XOpenDisplay.restype = [c.c_char_p], ptr
x.XDefaultRootWindow.argtypes, x.XDefaultRootWindow.restype = [ptr], ulong
x.XInternAtom.argtypes, x.XInternAtom.restype = [ptr, c.c_char_p, integer], ulong
x.XGetWindowProperty.argtypes = [ptr, ulong, ulong, c.c_long, c.c_long, integer, ulong,
                                c.POINTER(ulong), c.POINTER(integer), c.POINTER(ulong),
                                c.POINTER(ulong), c.POINTER(ptr)]
x.XFree.argtypes = [ptr]
x.XFlush.argtypes = [ptr]
x.XCloseDisplay.argtypes = [ptr]
x.XMoveWindow.argtypes = [ptr, ulong, integer, integer]
x.XResizeWindow.argtypes = [ptr, ulong, c.c_uint, c.c_uint]
class ClientMessage(c.Structure):
    _fields_ = [('type', integer), ('serial', ulong), ('send_event', integer), ('display', ptr),
                ('window', ulong), ('message_type', ulong), ('format', integer), ('data', c.c_long * 5)]


class XEvent(c.Union):
    _fields_ = [('client', ClientMessage), ('padding', c.c_long * 24)]


x.XSendEvent.argtypes = [ptr, ulong, integer, c.c_long, c.POINTER(XEvent)]
x.XGetGeometry.argtypes = [ptr, ulong, c.POINTER(ulong), c.POINTER(integer), c.POINTER(integer),
                           c.POINTER(c.c_uint), c.POINTER(c.c_uint), c.POINTER(c.c_uint), c.POINTER(c.c_uint)]
x.XTranslateCoordinates.argtypes = [ptr, ulong, ulong, integer, integer,
                                    c.POINTER(integer), c.POINTER(integer), c.POINTER(ulong)]
shape.XShapeGetRectangles.argtypes = [ptr, ulong, integer, c.POINTER(integer), c.POINTER(integer)]
shape.XShapeGetRectangles.restype = ptr
display = x.XOpenDisplay(None)
assert display, 'Cannot open the test XWayland display'
root = x.XDefaultRootWindow(display)
app_id = 'io.github._780APM.TwitchChatWindow'


def atom(name):
    return x.XInternAtom(display, name.encode(), 0)


def prop(window, name):
    actual, fmt, count, remaining, data = ulong(), integer(), ulong(), ulong(), ptr()
    result = x.XGetWindowProperty(display, window, atom(name), 0, 1024, 0, 0,
                                  c.byref(actual), c.byref(fmt), c.byref(count),
                                  c.byref(remaining), c.byref(data))
    assert result == 0, f'Cannot read {name}'
    if not data.value:
        return []
    try:
        if fmt.value == 8:
            return c.string_at(data, count.value)
        return list(c.cast(data, c.POINTER(ulong))[:count.value])
    finally:
        x.XFree(data)


def windows():
    return [w for w in prop(root, '_NET_CLIENT_LIST')
            if app_id.encode() in prop(w, 'WM_CLASS')]


def wait_for(check, description):
    deadline = time.monotonic() + 10
    while time.monotonic() < deadline:
        if check():
            return
        if app.poll() is not None:
            raise AssertionError(f'App exited during {description}: {app.returncode}')
        time.sleep(.05)
    raise AssertionError(f'Timed out: {description}')


def geometry():
    unused_root, child = ulong(), ulong()
    gx, gy, rx, ry = integer(), integer(), integer(), integer()
    width, height, border, depth = [c.c_uint() for _ in range(4)]
    assert x.XGetGeometry(display, window, c.byref(unused_root), c.byref(gx), c.byref(gy),
                          c.byref(width), c.byref(height), c.byref(border), c.byref(depth))
    assert x.XTranslateCoordinates(display, window, root, 0, 0, c.byref(rx), c.byref(ry), c.byref(child))
    return rx.value, ry.value, width.value, height.value


def input_rectangles():
    count, ordering = integer(), integer()
    rectangles = shape.XShapeGetRectangles(display, window, 2, c.byref(count), c.byref(ordering))
    if rectangles:
        x.XFree(rectangles)
    return count.value


def action(name):
    subprocess.run(['gapplication', 'action', app_id, name], check=True, timeout=10)


with open(os.path.join(os.environ['SMOKE_RUNTIME'], 'overlay.log'), 'w') as log:
    app = subprocess.Popen([os.environ['SMOKE_EXECUTABLE']], stdout=log, stderr=subprocess.STDOUT)
    try:
        wait_for(lambda: len(windows()) == 1, 'one application window')
        window = windows()[0]
        if os.environ['SMOKE_EXECUTABLE'].endswith('.AppImage'):
            data = Path(os.environ['XDG_DATA_HOME'])
            saved_image = data / 'twitch-chat-window/TwitchChatWindow.AppImage'
            launcher = data / 'applications/io.github._780APM.TwitchChatWindow.desktop'
            assert saved_image.is_file() and os.access(saved_image, os.X_OK), 'AppImage was not integrated'
            assert str(saved_image) in launcher.read_text(), 'Launcher does not use the saved AppImage'
            subprocess.run(['desktop-file-validate', str(launcher)], check=True, timeout=10)
            pid = prop(window, '_NET_WM_PID')[0]
            maps = Path(f'/proc/{pid}/maps').read_text()
            gtk = [line for line in maps.splitlines() if 'libgtk-3.so' in line]
            assert gtk and all('appimage_extracted_' in line or '/.mount_' in line for line in gtk), 'GTK came from the host instead of the AppImage'
        wait_for(lambda: atom('_NET_WM_STATE_ABOVE') in prop(window, '_NET_WM_STATE'), 'always on top')
        assert atom('_NET_WM_STATE_SKIP_TASKBAR') not in prop(window, '_NET_WM_STATE')
        assert atom('_NET_WM_WINDOW_TYPE_NORMAL') in prop(window, '_NET_WM_WINDOW_TYPE')
        for px, py in [(137, 93), (671, 311), (1407, 93), (41, 205)]:
            x.XMoveWindow(display, window, px, py)
            x.XFlush(display)
            wait_for(lambda: geometry()[:2] == (px, py), f'free position {px},{py}')
        x.XResizeWindow(display, window, 560, 390)
        x.XFlush(display)
        wait_for(lambda: geometry()[2:] == (560, 390), 'free resize')

        # Exercise the title's actual mouse handler, rather than only an X move request.
        px, py, _, _ = geometry()
        subprocess.run([os.environ['SMOKE_POINTER'], str(px + 100), str(py + 30), '160', '100'],
                       check=True, timeout=10)
        wait_for(lambda: geometry()[:2] != (px, py), 'dragging the title')

        action('toggle-click-through')
        wait_for(lambda: input_rectangles() == 0, 'click-through input region')
        # Unlock using real compositor-wide key events while a native Wayland
        # fullscreen client owns focus. Do not restart or activate the chat app.
        game_env = os.environ.copy()
        game_env['GDK_BACKEND'] = 'wayland'
        with open(os.path.join(os.environ['SMOKE_RUNTIME'], 'game.log'), 'w') as game_log:
            game = subprocess.Popen([os.environ['SMOKE_GAME']], env=game_env,
                                    stdout=game_log, stderr=subprocess.STDOUT)
            try:
                wait_for(lambda: prop(root, '_NET_ACTIVE_WINDOW') != [window], 'native Wayland game focus')
                subprocess.run([os.environ['SMOKE_POINTER'], 'unlock'], check=True, timeout=10)
                wait_for(lambda: input_rectangles() > 0, 'Ctrl+Shift+F10 unlock with another app focused')
                assert app.poll() is None and windows() == [window], 'Shortcut restarted the app'
                assert prop(root, '_NET_ACTIVE_WINDOW') != [window], 'Unlock stole game focus'
                subprocess.run([os.environ['SMOKE_POINTER'], 'unlock'], check=True, timeout=10)
                time.sleep(.1)
                assert input_rectangles() > 0, 'Pressing unlock again re-locked the window'
            finally:
                game.terminate()
                game.wait(timeout=10)
        action('toggle-click-through')
        wait_for(lambda: input_rectangles() == 0, 'lock after global shortcut')
        action('minimize')
        wait_for(lambda: atom('_NET_WM_STATE_HIDDEN') in prop(window, '_NET_WM_STATE'), 'taskbar minimize')
        # A taskbar asks the window manager to activate the existing window.
        event = XEvent()
        event.client = ClientMessage(33, 0, 1, display, window, atom('_NET_ACTIVE_WINDOW'), 32,
                                     (c.c_long * 5)(2, 0, 0, 0, 0))
        assert x.XSendEvent(display, root, 0, (1 << 19) | (1 << 20), c.byref(event))
        x.XFlush(display)
        wait_for(lambda: atom('_NET_WM_STATE_HIDDEN') not in prop(window, '_NET_WM_STATE'), 'taskbar restore')
        wait_for(lambda: input_rectangles() > 0, 'taskbar restore to unlock')
        action('toggle-click-through')
        wait_for(lambda: input_rectangles() == 0, 'click-through again')
        subprocess.run([os.environ['SMOKE_EXECUTABLE']], check=True, timeout=10)
        wait_for(lambda: input_rectangles() > 0, 'relaunch to unlock')
        assert windows() == [window], 'Relaunch created another window'
        action('quit')
        assert app.wait(timeout=10) == 0
        print('Single taskbar window, free movement, click-through, global unlock from native Wayland fullscreen, taskbar restore and quit passed.')
    finally:
        if app.poll() is None:
            app.terminate()
            app.wait(timeout=10)
        x.XCloseDisplay(display)
