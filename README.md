# (Testing) SMM II Comment Image Printer

## prerequistes

1. Arduino Uno R3
1. cp2102 USART to USB cable (USB to TTL?)
3. Awesome `Joystick` firmware from [wchill/SwitchInputEmulator](https://github.com/wchill/SwitchInputEmulator)

## Steps (自用)

### Compile and flash Joystick.hex
1. Refer to [wchill/SwitchInputEmulator](https://github.com/wchill/SwitchInputEmulator) and compile for `atmega16u2` which is only used for USB-Serial on Arduino Uno R3.
2. Go to `DFU mode` and flash `Joystick.hex` to `atmega16u2` chip on Arduino Uno R3.

### Wire up

#### PC to Arduino
Use USB to USART adapter, connect like below:

        USB to USART     Arduino Uno R3
        ------------     --------------
           GND                GND
           RXD                Pin0 (->RX)
           TXD                Pin1 (<-TX)
**Note: RXD should connect to Pin0 NOT Pin1**  
It took me 2 days to figure this out (I'm stupid). I just didn't realize PC should communicate with `atmega16u2` chip instead of the `Atmega328p`(which is the main chip of Arduino Uno R3).

#### Connect to Switch
**Note: Arduino is powered by this cable**  
Use the USB to serial cable that usually comes with the Arduino to connect to Switch Dock.

### Compile And Run
Change the `PortName` in App.cs to the correct port (Mine is "COM3") and run...

### Open Canvas and Draw
First click "加载" to load an image. 

    Any image should be fine. It will automatically scale and dither. Aspect ratio will be preserved
 
Use the normal switch controller to start Super Mario Maker 2, open a map and go to comment mode, pull out the controller. (For me, pull out the batteries of my wireless controller). And wait for a few seconds. It should display: "Press L+ R to select your controller". Press the "匹配" button on the main window of this App, and it should match successfully, then click "安排" to start printing.
