# (Testing) SMM II Comment Image Printer

## prerequistes

1. Arduino Uno R3
1. cp2102 USART to USB cable (USB to TTL?)
3. Awesome `Joystick` firmware from [wchill/SwitchInputEmulator](https://github.com/wchill/SwitchInputEmulator)

## Steps (自用)

### Compile and flash Joystick.hex
1. Refer to [wchill/SwitchInputEmulator](https://github.com/wchill/SwitchInputEmulator) and compile for `atmega16u2` which is only used for USB-Serial on Arduino Uno R3.
2. Go to `DFU mode` and flash `Joystick.hex` to `atmega16u2` chip on Arduino Uno R3.

**I had a problem using that code**: It seems that even if my PC stops sending any bytes, `atmega16u2` still keeps sending back `0x90` ( `RESP_USB_ACK`), which messed up my serial port logic. My workaround is as following.
#### workaround
go to `./Arduino/src/Joystick.c` and edit these places:

line 41:
~~~diff
USB_JoystickReport_Input_t defaultBuf;
State_t state = OUT_OF_SYNC;

+ bool isNewData = false;
+
ISR(USART1_RX_vect) {
    uint8_t b = recv_byte();
    if (state == SYNC_START) {
~~~
line 85:
~~~diff
                // send_byte(RESP_UPDATE_ACK);
+               isNewData = true;
            }
~~~
line 207:
~~~diff
        if (state == SYNCED) {                
            memcpy(&JoystickInputData, &buffer, sizeof(USB_JoystickReport_Input_t));
+            if (isNewData) {
+               isNewData = false;
                send_byte(RESP_USB_ACK);
+            }
        } else {
            memcpy(&JoystickInputData, &defaultBuf, sizeof(USB_JoystickReport_Input_t));
        }
~~~

### Wire up

#### PC to Arduino
Use USB to USART adapter, connect like below:

        USB to USART     Arduino Uno R3
        ------------     --------------
           GND                GND
           RXD                Pin0 (->RX)
           TXD                Pin1 (<-TX)
**Note: RXD should be connected to Pin0 NOT Pin1**  
It took me 2 days to figure this out (I'm stupid). I just didn't realize PC should communicate with `atmega16u2` chip instead of the `Atmega328p`(which is the main chip of Arduino Uno R3).

#### Connect to Switch
**Note: Arduino is powered by this cable**  
Use the USB to serial cable that usually comes with the Arduino to connect to Switch Dock.

### Compile And Run
Change the `PortName` in App.cs to the correct port (Mine is "COM3") and run...

### Open Canvas and Draw

#### Load image
First click "加载" to load an image. 

    Any image should work well. The App will automatically scale and dither the image, and its aspect ratio will be preserved.
 
#### Go to SMM II comment
Use a 3rd party? switch controller (I'm not using Joycons because I don't know how we can *disconnect* them) to start Super Mario Maker 2, open a map and go to comment mode.
#### Replace game controller
Pull out the controller you were using (For me, pull out the batteries of my wireless controller) and wait for a few seconds. It will display: "Press L+ R to select your controller". Press the "匹配" button on the main window of this App, and Arduino will match with the Switch successfully.
#### Start printing
Click "安排" to start printing.
