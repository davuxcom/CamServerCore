/*****************************************************************************
 * DirectShow Pan/Tilt/Zoom sample for Logitech QuickCam devices
 * 
 * Copyright 2007 (c) Logitech. All Rights Reserved.
 *
 * This code and information is provided "as is" without warranty of
 * any kind, either expressed or implied, including but not limited to
 * the implied warranties of merchantability and/or fitness for a
 * particular purpose.
 *
 * Version: 1.1
 ****************************************************************************/

#include <dshow.h>
#include <Ks.h>				// Required by KsMedia.h
#include <KsMedia.h>		// For KSPROPERTY_CAMERACONTROL_FLAGS_*


struct ControlInfo {
	long min;
	long max;
	long step;
	long def;
	long flags;
};

int iPan = 0;
int iTilt = 0;


/*
 * Print information about a control in an easily readable fashion.
 */
void print_control_info(ControlInfo *info)
{
	char flags[32] = "";

	if(info->flags & KSPROPERTY_CAMERACONTROL_FLAGS_AUTO)
	{
		strcat_s(flags, sizeof(flags), "AUTO | ");
	}
	else if(info->flags & KSPROPERTY_CAMERACONTROL_FLAGS_MANUAL)
	{
		strcat_s(flags, sizeof(flags), "MANUAL | ");
	}

	if(info->flags & KSPROPERTY_CAMERACONTROL_FLAGS_RELATIVE)
	{
		strcat_s(flags, sizeof(flags), "RELATIVE");
	}
	else
	{
		strcat_s(flags, sizeof(flags), "ABSOLUTE");
	}

	printf(
		"        min:   %d\n"
		"        max:   %d\n"
		"        step:  %d\n"
		"        def:   %d\n"
		"        flags: 0x%08X (%s)\n",
		info->min, info->max, info->step, info->def, info->flags, flags
	);
}


/*
 * Pans the camera by a given angle.
 *
 * The angle is given in degrees, positive values are clockwise rotation (seen from the top),
 * negative values are counter-clockwise rotation. If the "Mirror horizontal" option is
 * enabled, the panning sense is reversed.
 */
HRESULT set_mechanical_pan_relative(IAMCameraControl *pCameraControl, long value)
{
	HRESULT hr = 0;
	long flags = KSPROPERTY_CAMERACONTROL_FLAGS_RELATIVE | KSPROPERTY_CAMERACONTROL_FLAGS_MANUAL;

	hr = pCameraControl->Set(CameraControl_Pan, value, flags);
	if(hr != S_OK)
		fprintf(stderr, "ERROR: Unable to set CameraControl_Pan property value to %d. (Error 0x%08X)\n", value, hr);

	// Note that we need to wait until the movement is complete, otherwise the next request will
	// fail with hr == 0x800700AA == HRESULT_FROM_WIN32(ERROR_BUSY).
	Sleep(500);

	return hr;
}


/*
 * Tilts the camera by a given angle.
 *
 * The angle is given in degrees, positive values are downwards, negative values are upwards.
 * If the "Mirror vertical" option is enabled, the tilting sense is reversed.
 */
HRESULT set_mechanical_tilt_relative(IAMCameraControl *pCameraControl, long value)
{
	HRESULT hr = 0;
	long flags = KSPROPERTY_CAMERACONTROL_FLAGS_RELATIVE | KSPROPERTY_CAMERACONTROL_FLAGS_MANUAL;

	hr = pCameraControl->Set(CameraControl_Tilt, value, flags);
	if(hr != S_OK)
		fprintf(stderr, "ERROR: Unable to set CameraControl_Tilt property value to %d. (Error 0x%08X)\n", value, hr);

	// Note that we need to wait until the movement is complete, otherwise the next request will
	// fail with hr == 0x800700AA == HRESULT_FROM_WIN32(ERROR_BUSY).
	Sleep(500);

	return hr;
}


/*
 * Resets the camera's pan/tilt position by moving into a corner and then back to the center.
 */
void reset_machanical_pan_tilt(IAMCameraControl *pCameraControl)
{
	set_mechanical_pan_relative(pCameraControl, 180);
	Sleep(500);
	set_mechanical_tilt_relative(pCameraControl, 180);
	Sleep(500);
	set_mechanical_pan_relative(pCameraControl, -64);
	Sleep(500);
	set_mechanical_tilt_relative(pCameraControl, -24);
	Sleep(500);
}


/*
 * Sets the digital pan angle.
 *
 * Positive values pan to the right, negative values pan to the left. Note that the digital pan
 * angle only has an influence if the digital zoom is active.
 */
HRESULT set_digital_pan_absolute(IAMCameraControl *pCameraControl, long value)
{
	HRESULT hr = 0;

	// Specifying the KSPROPERTY_CAMERACONTROL_FLAGS_ABSOLUTE flag instructs the driver
	// to use digital instead of mechanical pan.
	long flags = KSPROPERTY_CAMERACONTROL_FLAGS_ABSOLUTE | KSPROPERTY_CAMERACONTROL_FLAGS_MANUAL;

	hr = pCameraControl->Set(CameraControl_Pan, value, flags);
	if(hr != S_OK)
		fprintf(stderr, "ERROR: Unable to set CameraControl_Pan property value to %d. (Error 0x%08X)\n", value, hr);

	return hr;
}


/*
 * Sets the digital tilt angle.
 *
 * Positive values tilt downwards, negative values tilt upwards. Note that the digital pan
 * angle only has an influence if the digital zoom is active.
 */
HRESULT set_digital_tilt_absolute(IAMCameraControl *pCameraControl, long value)
{
	HRESULT hr = 0;

	// Specifying the KSPROPERTY_CAMERACONTROL_FLAGS_ABSOLUTE flag instructs the driver
	// to use digital instead of mechanical tilt.
	long flags = KSPROPERTY_CAMERACONTROL_FLAGS_ABSOLUTE | KSPROPERTY_CAMERACONTROL_FLAGS_MANUAL;

	hr = pCameraControl->Set(CameraControl_Tilt, value, flags);
	if(hr != S_OK)
		fprintf(stderr, "ERROR: Unable to set CameraControl_Tilt property value to %d. (Error 0x%08X)\n", value, hr);

	return hr;
}


/*
 * Sets the digital zoom value.
 *
 * The minimum value is 50 and means no zoom (100%). The maximum value is 200
 * and means 4x zoom (400%).
 */
HRESULT set_digital_zoom_absolute(IAMCameraControl *pCameraControl, long value)
{
	HRESULT hr = 0;
	long flags = KSPROPERTY_CAMERACONTROL_FLAGS_ABSOLUTE | KSPROPERTY_CAMERACONTROL_FLAGS_MANUAL;

	hr = pCameraControl->Set(CameraControl_Zoom, value, flags);
	if(hr != S_OK)
		fprintf(stderr, "ERROR: Unable to set CameraControl_Zoom property value to %d. (Error 0x%08X)\n", value, hr);

	return hr;
}


/*
 * Resets the digital pan and tilt angles.
 */
void reset_digital_pan_tilt(IAMCameraControl *pCameraControl)
{
	set_digital_pan_absolute(pCameraControl, 0);
	set_digital_tilt_absolute(pCameraControl, 0);
}


/*
 * Resets the digital zoom.
 */
void reset_digital_zoom(IAMCameraControl *pCameraControl)
{
	set_digital_zoom_absolute(pCameraControl, 50);
}


/*
 * Test a camera's pan/tilt properties
 *
 * See also:
 *
 * IAMCameraControl Interface
 *     http://msdn2.microsoft.com/en-us/library/ms783833.aspx
 * PROPSETID_VIDCAP_CAMERACONTROL
 *     http://msdn2.microsoft.com/en-us/library/aa510754.aspx
 */
HRESULT test_pan_tilt(IBaseFilter *pBaseFilter)
{
	HRESULT hr = 0;
	IAMCameraControl *pCameraControl = NULL;
	ControlInfo panInfo = { 0 };
	ControlInfo tiltInfo = { 0 };
	ControlInfo zoomInfo = { 0 };
	long value = 0, flags = 0;

	printf("    Reading pan/tilt property information ...\n");

	// Get a pointer to the IAMCameraControl interface used to control the camera
	hr = pBaseFilter->QueryInterface(IID_IAMCameraControl, (void **)&pCameraControl);
	if(hr != S_OK)
	{
		fprintf(stderr, "ERROR: Unable to access IAMCameraControl interface.\n");
		return hr;
	}

	// Retrieve information about the pan and tilt controls
	hr = pCameraControl->GetRange(CameraControl_Pan, &panInfo.min, &panInfo.max, &panInfo.step, &panInfo.def, &panInfo.flags);
	if(hr != S_OK)
	{
		fprintf(stderr, "ERROR: Unable to retrieve CameraControl_Pan property information.\n");
		return hr;
	}
	printf("      Pan control:\n");
	print_control_info(&panInfo);

	hr = pCameraControl->GetRange(CameraControl_Tilt, &tiltInfo.min, &tiltInfo.max, &tiltInfo.step, &tiltInfo.def, &tiltInfo.flags);
	if(hr != S_OK)
	{
		fprintf(stderr, "ERROR: Unable to retrieve CameraControl_Tilt property information.\n");
		return hr;
	}
	printf("      Tilt control:\n");
	print_control_info(&tiltInfo);

		//reset_machanical_pan_tilt(pCameraControl);
		//Sleep(3000);
	set_mechanical_pan_relative(pCameraControl, iPan);
	set_mechanical_tilt_relative(pCameraControl, iTilt);

	/*

	printf("    Resetting pan/tilt/zoom ...\n");
	reset_machanical_pan_tilt(pCameraControl);
	reset_digital_pan_tilt(pCameraControl);
	reset_digital_zoom(pCameraControl);
	Sleep(3000);



	printf("    Testing mechanical pan ...\n");
	set_mechanical_pan_relative(pCameraControl, 40);
	set_mechanical_pan_relative(pCameraControl, 20);
	set_mechanical_pan_relative(pCameraControl, -20);
	set_mechanical_pan_relative(pCameraControl, -40);
	Sleep(3000);


	//*
	printf("    Testing mechanical tilt ...\n");
	set_mechanical_tilt_relative(pCameraControl, 20);
	set_mechanical_tilt_relative(pCameraControl, 10);
	set_mechanical_tilt_relative(pCameraControl, -10);
	set_mechanical_tilt_relative(pCameraControl, -20);
	Sleep(3000);



	printf("    Testing digital pan/tilt/zoom ...\n");
	set_digital_zoom_absolute(pCameraControl, 100);		// Zoom to 200%
	Sleep(1000);

	set_digital_pan_absolute(pCameraControl, 40);
	Sleep(1000);
	set_digital_pan_absolute(pCameraControl, 80);
	Sleep(1000);

	set_digital_zoom_absolute(pCameraControl, 200);		// Zoom to 400%
	Sleep(1000);

	set_digital_tilt_absolute(pCameraControl, 40);
	Sleep(1000);
	set_digital_tilt_absolute(pCameraControl, 60);
	Sleep(1000);
	
	reset_digital_pan_tilt(pCameraControl);
	Sleep(1000);
	reset_digital_zoom(pCameraControl);
	Sleep(3000);
	//*/


	return S_OK;
}


/*
 * Do something with the filter. In this sample we just test the pan/tilt properties.
 */
void process_filter(IBaseFilter *pBaseFilter)
{
	test_pan_tilt(pBaseFilter);
}


/*
 * Enumerate all video devices
 *
 * See also:
 *
 * Using the System Device Enumerator:
 *     http://msdn2.microsoft.com/en-us/library/ms787871.aspx
 */
int enum_devices()
{
	HRESULT hr;

	printf("Enumerating video input devices ...\n");

	// Create the System Device Enumerator.
	ICreateDevEnum *pSysDevEnum = NULL;
	hr = CoCreateInstance(CLSID_SystemDeviceEnum, NULL, CLSCTX_INPROC_SERVER,
		IID_ICreateDevEnum, (void **)&pSysDevEnum);
	if(FAILED(hr))
	{
		fprintf(stderr, "ERROR: Unable to create system device enumerator.\n");
		return hr;
	}

	// Obtain a class enumerator for the video input device category.
	IEnumMoniker *pEnumCat = NULL;
	hr = pSysDevEnum->CreateClassEnumerator(CLSID_VideoInputDeviceCategory, &pEnumCat, 0);

	if(hr == S_OK) 
	{
		// Enumerate the monikers.
		IMoniker *pMoniker = NULL;
		ULONG cFetched;
		while(pEnumCat->Next(1, &pMoniker, &cFetched) == S_OK)
		{
			IPropertyBag *pPropBag;
			hr = pMoniker->BindToStorage(0, 0, IID_IPropertyBag, 
				(void **)&pPropBag);
			if(SUCCEEDED(hr))
			{
				// To retrieve the filter's friendly name, do the following:
				VARIANT varName;
				VariantInit(&varName);
				hr = pPropBag->Read(L"FriendlyName", &varName, 0);
				if (SUCCEEDED(hr))
				{
					// Display the name in your UI somehow.
					wprintf(L"  Found device: %s\n", varName.bstrVal);
				}
				VariantClear(&varName);

				// To create an instance of the filter, do the following:
				IBaseFilter *pFilter;
				hr = pMoniker->BindToObject(NULL, NULL, IID_IBaseFilter,
					(void**)&pFilter);
				
				process_filter(pFilter);

				//Remember to release pFilter later.
				pPropBag->Release();
			}
			pMoniker->Release();
		}
		pEnumCat->Release();
	}
	pSysDevEnum->Release();

	return 0;
}


int wmain(int argc, wchar_t* argv[])
{
	int result;

	if (argc == 3)
	{
		iPan = _wtoi(argv[1]);
		iTilt = _wtoi(argv[2]);
	}
	else
	{
		printf("Wrong arg count\n\n");
	}

	CoInitializeEx(NULL, COINIT_APARTMENTTHREADED);

	result = enum_devices();

	CoUninitialize();

	return result;
}
