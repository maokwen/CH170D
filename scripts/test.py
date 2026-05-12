import time

import pywinusb.hid as hid

VID = 0x3633
PID = 0x0013

filter = hid.HidDeviceFilter(vendor_id=VID, product_id=PID)
devices = filter.get_devices()

if not devices:
    print("device not found")
    exit()

device = devices[0]
device.open()

# print(f"Connected: {device.product_name}")
# print(f"Reports: {device.find_output_reports()}")
#
reports = device.find_output_reports()
if reports:
    report = reports[0]
    # raw = bytes.fromhex("1068010623010200b700427400005e096000000010424800000700d20000000000000000000000003c")

    raw = bytes.fromhex(
        "680106230103001B0042480000050ABF03AE000F426000000100D2000000000000000000000000"
    )
    raw += bytes.fromhex(f"{sum(raw) & 0xFF:02x}")

    data = (
        bytes.fromhex("10")
        + raw
        + bytes.fromhex("1600000000000000000000000000000000000000000000")
    )

    report.set_raw_data(list(data))
    report.send()
    print("Sent")

device.close()
