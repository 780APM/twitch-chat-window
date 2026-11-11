/* Test-only pointer injection into the smoke test's isolated KWin session. */
#include <stdint.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>
#include <wayland-client.h>
#include "fake-input-client.h"

static struct org_kde_kwin_fake_input *input;
static void global(void *data, struct wl_registry *registry, uint32_t name,
                   const char *interface, uint32_t version)
{
    (void)data;
    if (!strcmp(interface, "org_kde_kwin_fake_input") && version >= 3)
        input = wl_registry_bind(registry, name, &org_kde_kwin_fake_input_interface, 3);
}
static void removed(void *data, struct wl_registry *registry, uint32_t name)
{
    (void)data; (void)registry; (void)name;
}
int main(int argc, char **argv)
{
    if (argc != 5) return 2;
    struct wl_display *display = wl_display_connect(NULL);
    if (!display) return 1;
    struct wl_registry *registry = wl_display_get_registry(display);
    const struct wl_registry_listener listener = {global, removed};
    wl_registry_add_listener(registry, &listener, NULL);
    if (wl_display_roundtrip(display) < 0 || !input) return 1;
    org_kde_kwin_fake_input_authenticate(input, "Twitch Chat smoke test", "Verify title dragging in isolated KWin");
    org_kde_kwin_fake_input_pointer_motion_absolute(input,
        wl_fixed_from_int(atoi(argv[1])), wl_fixed_from_int(atoi(argv[2])));
    wl_display_roundtrip(display);
    usleep(200000);
    org_kde_kwin_fake_input_button(input, 0x110, 1);
    wl_display_roundtrip(display);
    usleep(100000);
    for (int step = 0; step < 10; step++) {
        org_kde_kwin_fake_input_pointer_motion(input,
            wl_fixed_from_double(atof(argv[3]) / 10), wl_fixed_from_double(atof(argv[4]) / 10));
        wl_display_roundtrip(display);
        usleep(30000);
    }
    org_kde_kwin_fake_input_button(input, 0x110, 0);
    wl_display_roundtrip(display);
    wl_proxy_destroy((struct wl_proxy *)input);
    wl_registry_destroy(registry);
    wl_display_disconnect(display);
    return 0;
}
