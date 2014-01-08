DirectShow Pan/Tilt/Zoom sample for Logitech QuickCam devices
=============================================================

Copyright 2007 (c) Logitech. All Rights Reserved.

This code and information is provided "as is" without warranty of
any kind, either expressed or implied, including but not limited to
the implied warranties of merchantability and/or fitness for a
particular purpose.


Introduction
============

This C++ sample demonstrates how to use the mechanical pan/tilt and digital pan/tilt/zoom
capabilities of some Logitech QuickCam camera devices.

At the time of this writing (October 2007) mechanical pan/tilt is supported by the following
camera models:

Logitech QuickCam Orbit/Sphere MP (PID 08C2)
Logitech QuickCam Orbit/Sphere MP (2006 model, PID 08CC)
Logitech QuickCam Orbit/Sphere AF (PID 0994)

Digital pan/tilt/zoom is supported both by cameras with and without mechanical pan/tilt.

There is a how-to article to go with this sample. You can find it here:
http://www.quickcamteam.net/documentation/how-to/how-to-control-pan-tilt-zoom-on-logitech-cameras


Description
===========

The sample uses the standard DirectShow API to enumerate all video input devices in the system
and to access the corresponding camera control properties.

To see the sample in action, either run the provided PTZ.exe in the Release directory or compile
the sample code and run the resulting PTZ.exe from a command line to see the output. In order
to be able to see the effects of digital pan/tilt/zoom it is recommended to start a video
stream in a video application like AMCap before running the tool. The mechanical pan/tilt
obviously works whether the camera is streaming video or not.

Used DirectShow property information:

Interface:    IAMCameraControl
Property set: PROPSETID_VIDCAP_CAMERACONTROL
Properties:   KSPROPERTY_CAMERACONTROL_PAN
              KSPROPERTY_CAMERACONTROL_TILT
              KSPROPERTY_CAMERACONTROL_ZOOM

These properties are generic properties defined by Microsoft. The following behavior, however,
is specific to the Logitech UVC driver:

- Specifying the KSPROPERTY_CAMERACONTROL_FLAGS_ABSOLUTE flag for the pan and tilt properties
  instructs the driver to use digital instead of mechanical pan/tilt.

For further information please refer to the MSDN documentation on DirectShow and the inline
code comments and links.

If you have questions or problems with this sample, you can post them in the QuickCam Team
Windows Webcam Development forum at: http://forums.quickcamteam.net/
We do not guarantee support for this sample but we will try to respond to your questions.


Prerequisites
=============

For compilation:
- Visual Studio 2005
- A recent version of the Windows SDK (tested with 6.0 and 6.1)

The tool was tested on a Windows XP with SP2 and Windows Vista.


Release history
===============

1.0: Initial internal release

1.1: First release to the public
