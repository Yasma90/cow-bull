using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace CowBullClient.Model
{
    public class Mixer
    {
        #region Constants
        private const int MAXPNAMELEN = 32;
        private const int MIXER_LONG_NAME_CHARS = 64;
        private const int MIXER_SHORT_NAME_CHARS = 16;
        private const int MIXER_GETLINECONTROLSF_ONEBYTYPE = 0x2;
        private const int MIXERLINE_COMPONENTTYPE_DST_FIRST = 0x0;
        private const int MIXERLINE_COMPONENTTYPE_SRC_FIRST = 0x1000;
        private const int MIXERLINE_COMPONENTTYPE_DST_SPEAKERS = (MIXERLINE_COMPONENTTYPE_DST_FIRST + 4);
        private const int MIXERLINE_COMPONENTTYPE_SRC_MICROPHONE = (MIXERLINE_COMPONENTTYPE_SRC_FIRST + 3);
        private const int MIXERLINE_COMPONENTTYPE_SRC_LINE = (MIXERLINE_COMPONENTTYPE_SRC_FIRST + 2);
        private const int MIXERCONTROL_CT_CLASS_FADER = 0x50000000;
        private const int MIXERCONTROL_CT_UNITS_UNSIGNED = 0x30000;
        private const int MIXERCONTROL_CONTROLTYPE_FADER = (MIXERCONTROL_CT_CLASS_FADER | MIXERCONTROL_CT_UNITS_UNSIGNED);
        private const int MIXERCONTROL_CONTROLTYPE_VOLUME = (MIXERCONTROL_CONTROLTYPE_FADER + 1);
        private const UInt32 MEM_COMMIT = 0x1000;
        private const UInt32 PAGE_READWRITE = 0x04;
        private const UInt32 MEM_RELEASE = 0x8000;
        #endregion

        #region Enums
        public enum MMSYSERR : int
        {
            NOERROR = 0,
            ERROR,
            BADDEVICEID,
            NOTENABLED,
            ALLOCATED,
            INVALHANDLE,
            NODRIVER,
            NOMEM,
            NOTSUPPORTED,
            BADERRNUM,
            INVALFLAG,
            INVALPARAM,
            HANDLEBUSY,
            INVALIDALIAS,
            BADDB,
            KEYNOTFOUND,
            READERROR,
            WRITEERROR,
            DELETEERROR,
            VALNOTFOUND,
            NODRIVERCB,
        }

        [Flags]
        private enum MIXER_GETLINEINFOF
        {
            DESTINATION = 0x00000000,
            SOURCE = 0x00000001,
            LINEID = 0x00000002,
            COMPONENTTYPE = 0x00000003,
            TARGETTYPE = 0x00000004,
            QUERYMASK = 0x0000000F
        }

        [Flags]
        private enum MIXERLINE_COMPONENTTYPE : uint
        {
            DST_FIRST = 0x00000000,
            DST_UNDEFINED = (DST_FIRST + 0),
            DST_DIGITAL = (DST_FIRST + 1),
            DST_LINE = (DST_FIRST + 2),
            DST_MONITOR = (DST_FIRST + 3),
            DST_SPEAKERS = (DST_FIRST + 4),
            DST_HEADPHONES = (DST_FIRST + 5),
            DST_TELEPHONE = (DST_FIRST + 6),
            DST_WAVEIN = (DST_FIRST + 7),
            DST_VOICEIN = (DST_FIRST + 8),
            DST_LAST = (DST_FIRST + 8),
            SRC_FIRST = 0x00001000,
            SRC_UNDEFINED = (SRC_FIRST + 0),
            SRC_DIGITAL = (SRC_FIRST + 1),
            SRC_LINE = (SRC_FIRST + 2),
            SRC_MICROPHONE = (SRC_FIRST + 3),
            SRC_SYNTHESIZER = (SRC_FIRST + 4),
            SRC_COMPACTDISC = (SRC_FIRST + 5),
            SRC_TELEPHONE = (SRC_FIRST + 6),
            SRC_PCSPEAKER = (SRC_FIRST + 7),
            SRC_WAVEOUT = (SRC_FIRST + 8),
            SRC_AUXILIARY = (SRC_FIRST + 9),
            SRC_ANALOG = (SRC_FIRST + 10),
            SRC_LAST = (SRC_FIRST + 10)
        }

        [Flags]
        private enum MIXERCONTROL_CT_CLASS : uint
        {
            MASK = 0xF0000000,
            CUSTOM = 0x00000000,
            METER = 0x10000000,
            SWITCH = 0x20000000,
            NUMBER = 0x30000000,
            SLIDER = 0x40000000,
            FADER = 0x50000000,
            TIME = 0x60000000,
            LIST = 0x70000000,
            MIXERCONTROL_CT_SUBCLASS_MASK = 0x0F000000,
            MIXERCONTROL_CT_SC_SWITCH_BOOLEAN = 0x00000000,
            MIXERCONTROL_CT_SC_SWITCH_BUTTON = 0x01000000,
            MIXERCONTROL_CT_SC_METER_POLLED = 0x00000000,
            MIXERCONTROL_CT_SC_TIME_MICROSECS = 0x00000000,
            MIXERCONTROL_CT_SC_TIME_MILLISECS = 0x01000000,
            MIXERCONTROL_CT_SC_LIST_SINGLE = 0x00000000,
            MIXERCONTROL_CT_SC_LIST_MULTIPLE = 0x01000000,
            MIXERCONTROL_CT_UNITS_MASK = 0x00FF0000,
            MIXERCONTROL_CT_UNITS_CUSTOM = 0x00000000,
            MIXERCONTROL_CT_UNITS_BOOLEAN = 0x00010000,
            MIXERCONTROL_CT_UNITS_SIGNED = 0x00020000,
            MIXERCONTROL_CT_UNITS_UNSIGNED = 0x00030000,
            MIXERCONTROL_CT_UNITS_DECIBELS = 0x00040000,
            MIXERCONTROL_CT_UNITS_PERCENT = 0x00050000,
        }

        [Flags]
        private enum MIXERCONTROL_CONTROLTYPE : uint
        {
            CUSTOM = (MIXERCONTROL_CT_CLASS.CUSTOM | MIXERCONTROL_CT_CLASS.MIXERCONTROL_CT_UNITS_CUSTOM),
            BOOLEANMETER = (MIXERCONTROL_CT_CLASS.METER | MIXERCONTROL_CT_CLASS.MIXERCONTROL_CT_SC_METER_POLLED | MIXERCONTROL_CT_CLASS.MIXERCONTROL_CT_UNITS_BOOLEAN),
            SIGNEDMETER = (MIXERCONTROL_CT_CLASS.METER | MIXERCONTROL_CT_CLASS.MIXERCONTROL_CT_SC_METER_POLLED | MIXERCONTROL_CT_CLASS.MIXERCONTROL_CT_UNITS_SIGNED),
            PEAKMETER = (SIGNEDMETER + 1),
            UNSIGNEDMETER = (MIXERCONTROL_CT_CLASS.METER | MIXERCONTROL_CT_CLASS.MIXERCONTROL_CT_SC_METER_POLLED | MIXERCONTROL_CT_CLASS.MIXERCONTROL_CT_UNITS_UNSIGNED),
            BOOLEAN = (MIXERCONTROL_CT_CLASS.SWITCH | MIXERCONTROL_CT_CLASS.MIXERCONTROL_CT_SC_SWITCH_BOOLEAN | MIXERCONTROL_CT_CLASS.MIXERCONTROL_CT_UNITS_BOOLEAN),
            ONOFF = (BOOLEAN + 1),
            MUTE = (BOOLEAN + 2),
            MONO = (BOOLEAN + 3),
            LOUDNESS = (BOOLEAN + 4),
            STEREOENH = (BOOLEAN + 5),
            BASS_BOOST = (BOOLEAN + 0x00002277),
            BUTTON = (MIXERCONTROL_CT_CLASS.SWITCH | MIXERCONTROL_CT_CLASS.MIXERCONTROL_CT_SC_SWITCH_BUTTON | MIXERCONTROL_CT_CLASS.MIXERCONTROL_CT_UNITS_BOOLEAN),
            DECIBELS = (MIXERCONTROL_CT_CLASS.NUMBER | MIXERCONTROL_CT_CLASS.MIXERCONTROL_CT_UNITS_DECIBELS),
            SIGNED = (MIXERCONTROL_CT_CLASS.NUMBER | MIXERCONTROL_CT_CLASS.MIXERCONTROL_CT_UNITS_SIGNED),
            UNSIGNED = (MIXERCONTROL_CT_CLASS.NUMBER | MIXERCONTROL_CT_CLASS.MIXERCONTROL_CT_UNITS_UNSIGNED),
            PERCENT = (MIXERCONTROL_CT_CLASS.NUMBER | MIXERCONTROL_CT_CLASS.MIXERCONTROL_CT_UNITS_PERCENT),
            SLIDER = (MIXERCONTROL_CT_CLASS.SLIDER | MIXERCONTROL_CT_CLASS.MIXERCONTROL_CT_UNITS_SIGNED),
            PAN = (SLIDER + 1),
            QSOUNDPAN = (SLIDER + 2),
            FADER = (MIXERCONTROL_CT_CLASS.FADER | MIXERCONTROL_CT_CLASS.MIXERCONTROL_CT_UNITS_UNSIGNED),
            VOLUME = (FADER + 1),
            BASS = (FADER + 2),
            TREBLE = (FADER + 3),
            EQUALIZER = (FADER + 4),
            SINGLESELECT = (MIXERCONTROL_CT_CLASS.LIST | MIXERCONTROL_CT_CLASS.MIXERCONTROL_CT_SC_LIST_SINGLE | MIXERCONTROL_CT_CLASS.MIXERCONTROL_CT_UNITS_BOOLEAN),
            MUX = (SINGLESELECT + 1),
            MULTIPLESELECT = (MIXERCONTROL_CT_CLASS.LIST | MIXERCONTROL_CT_CLASS.MIXERCONTROL_CT_SC_LIST_MULTIPLE | MIXERCONTROL_CT_CLASS.MIXERCONTROL_CT_UNITS_BOOLEAN),
            MIXER = (MULTIPLESELECT + 1),
            MICROTIME = (MIXERCONTROL_CT_CLASS.TIME | MIXERCONTROL_CT_CLASS.MIXERCONTROL_CT_SC_TIME_MICROSECS | MIXERCONTROL_CT_CLASS.MIXERCONTROL_CT_UNITS_UNSIGNED),
            MILLITIME = (MIXERCONTROL_CT_CLASS.TIME | MIXERCONTROL_CT_CLASS.MIXERCONTROL_CT_SC_TIME_MILLISECS | MIXERCONTROL_CT_CLASS.MIXERCONTROL_CT_UNITS_UNSIGNED)
        }

        [Flags]
        enum MIXERLINE_LINEF : uint
        {
            ACTIVE = 0x00000001,
            DISCONNECTED = 0x00008000,
            SOURCE = 0x80000000
        }
        #endregion

        #region API
        [DllImport("winmm.dll")]
        private static extern MMSYSERR mixerClose(IntPtr hmx);

        [DllImport("winmm.dll", SetLastError = true)]
        private static extern MMSYSERR mixerGetLineControls(IntPtr hmxobj, ref MIXERLINECONTROLS pmxlc, UInt32 fdwControls);

        [DllImport("winmm.dll", SetLastError = true)]
        private static extern MMSYSERR mixerGetLineInfo(IntPtr hmxobj, ref MIXERLINE pmxl, MIXER_GETLINEINFOF fdwInfo);

        [DllImport("winmm.dll")]
        private static extern MMSYSERR mixerOpen(ref IntPtr phmx, uint pMxId, IntPtr dwCallback, IntPtr dwInstance, UInt32 fdwOpen);

        [DllImport("winmm.dll")]
        private static extern MMSYSERR mixerSetControlDetails(IntPtr hmxobj, ref MIXERCONTROLDETAILS pmxcd, UInt32 fdwDetails);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAlloc(IntPtr lpAddress, UInt32 dwSize, UInt32 flAllocationType, UInt32 flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern Boolean VirtualFree(IntPtr lpAddress, UInt32 dwSize, UInt32 dwFreeType);
        #endregion

        #region Structs
        [StructLayout(LayoutKind.Sequential, Size = 20, Pack = 4)]
        private struct MIXERCONTROLDETAILS
        {
            public UInt32 cbStruct;
            public UInt32 dwControlID;
            public UInt32 cChannels;
            public IntPtr hwndOwner;
            public UInt32 cbDetails;
            public IntPtr paDetails;
        }

        [StructLayout(LayoutKind.Sequential, Size = 4, Pack = 4)]
        private struct MIXERCONTROLDETAILS_UNSIGNED
        {
            public UInt32 value;
        }

        [StructLayout(LayoutKind.Sequential, Size = 168, CharSet = CharSet.Ansi, Pack = 4)]
        private struct MIXERLINE
        {
            public UInt32 cbStruct;
            public UInt32 dwDestination;
            public UInt32 dwSource;
            public UInt32 dwLineID;
            public UInt32 fdwLine;
            public UInt32 dwUser;
            public UInt32 dwComponentType;
            public UInt32 cChannels;
            public UInt32 cConnections;
            public UInt32 cControls;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = MIXER_SHORT_NAME_CHARS)]
            public char[] pShortName;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = MIXER_LONG_NAME_CHARS)]
            public char[] pName;
            public UInt32 dwType;
            public UInt32 dwDeviceID;
            public UInt16 wMid;
            public UInt16 wPid;
            public UInt32 vDriverVersion;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = MAXPNAMELEN)]
            public char[] szPname;
        }

        [StructLayout(LayoutKind.Sequential, Size = 24, Pack = 4)]
        private struct MIXERLINECONTROLS
        {
            public UInt32 cbStruct;
            public UInt32 dwLineID;
            public UInt32 dwControlID;
            public UInt32 cControls;
            public UInt32 cbmxctrl;
            public IntPtr pamxctrl;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Size = 148, Pack = 4)]
        private struct MIXERCONTROL
        {
            public UInt32 cbStruct;
            public UInt32 dwControlID;
            public UInt32 dwControlType;
            public UInt32 fdwControl;
            public UInt32 cMultipleItems;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = MIXER_SHORT_NAME_CHARS)]
            public char[] szShortName;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = MIXER_LONG_NAME_CHARS)]
            public char[] szName;
            public UInt32 dwMinimum;
            public UInt32 dwMaximum;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
            public UInt32[] reserved;
        }
        #endregion

        #region Fields
        private int _iVolNorm = 0;
        private IntPtr _hMixer = IntPtr.Zero;
        private MIXERCONTROL _tVolume;
        #endregion

        #region Public Methods
        /// <summary>Open the mixer</summary>
        /// <returns>error</returns>
        public MMSYSERR OpenMixer()
        {
            MMSYSERR err = MMSYSERR.NOERROR;

            err = mixerOpen(ref _hMixer, 0, IntPtr.Zero, IntPtr.Zero, 0);
            if (err == MMSYSERR.NOERROR)
            {
                _tVolume = new MIXERCONTROL();
                _tVolume.cbStruct = (uint)Marshal.SizeOf(_tVolume);

                err = GetVolumeInfo(_hMixer, MIXERCONTROL_CONTROLTYPE_VOLUME, ref _tVolume);
                if (err == MMSYSERR.NOERROR)
                {
                    _iVolNorm = (int)_tVolume.fdwControl;
                    return err;
                }
            }
            if (err != MMSYSERR.NOERROR && _hMixer != IntPtr.Zero)
                CloseMixer();
            return err;
        }

        /// <summary>set volume level</summary>
        /// <param name="uVolume">level</param>
        /// <returns>error</returns>
        public MMSYSERR SetVolume(uint uVolume)
        {
            return SetVolume(_hMixer, _tVolume, uVolume);
        }

        /// <summary>Close the mixer</summary>
        public void CloseMixer()
        {
            if (_hMixer != IntPtr.Zero)
            {
                mixerClose(_hMixer);
                _hMixer = IntPtr.Zero;
            }
        }
        #endregion

        #region Private Methods
        private MMSYSERR GetVolumeInfo(IntPtr hmixer, int ctrlType, ref MIXERCONTROL mxc)
        {
            MMSYSERR err = MMSYSERR.NOERROR;
            try
            {
                IntPtr hmem = IntPtr.Zero;
                MIXERLINECONTROLS mxlc = new MIXERLINECONTROLS();
                mxlc.cbStruct = (uint)Marshal.SizeOf(mxlc);
                MIXERLINE mxl = new MIXERLINE();
                mxl.cbStruct = (uint)Marshal.SizeOf(mxl);
                mxl.dwComponentType = (uint)MIXERLINE_COMPONENTTYPE.DST_SPEAKERS;
                err = mixerGetLineInfo(hmixer, ref mxl, MIXER_GETLINEINFOF.COMPONENTTYPE);

                if (err == MMSYSERR.NOERROR)
                {
                    mxlc.dwLineID = (uint)mxl.dwLineID;
                    mxlc.dwControlID = (uint)ctrlType;
                    mxlc.cControls = 1;
                    mxlc.cbmxctrl = (uint)Marshal.SizeOf(mxc);
                    hmem = malloc(Marshal.SizeOf(mxlc));
                    mxlc.pamxctrl = hmem;
                    mxc.cbStruct = (uint)Marshal.SizeOf(mxc);
                    err = mixerGetLineControls(hmixer, ref mxlc, MIXER_GETLINECONTROLSF_ONEBYTYPE);

                    if (err == MMSYSERR.NOERROR)
                    {
                        mxc = (MIXERCONTROL)Marshal.PtrToStructure(mxlc.pamxctrl, typeof(MIXERCONTROL));
                        if (hmem != IntPtr.Zero)
                            free(hmem, Marshal.SizeOf(mxc));
                        return err;
                    }
                    if (hmem != IntPtr.Zero)
                        free(hmem, Marshal.SizeOf(mxc));
                }
                return err;
            }
            catch { return err; }
        }

        private IntPtr malloc(int size)
        {
            return VirtualAlloc(IntPtr.Zero, (uint)size, MEM_COMMIT, PAGE_READWRITE);
        }

        private void free(IntPtr m, int size)
        {
            VirtualFree(m, (uint)size, MEM_RELEASE);
        }

        private MMSYSERR SetVolume(IntPtr hmixer, MIXERCONTROL mxc, uint volume)
        {
            IntPtr hmem = IntPtr.Zero;
            MMSYSERR err = MMSYSERR.NOERROR;
            MIXERCONTROLDETAILS mxcd = new MIXERCONTROLDETAILS();
            MIXERCONTROLDETAILS_UNSIGNED vol = new MIXERCONTROLDETAILS_UNSIGNED();

            try
            {
                mxcd.hwndOwner = IntPtr.Zero;
                mxcd.dwControlID = mxc.dwControlID;
                mxcd.cbStruct = (uint)Marshal.SizeOf(mxcd);
                mxcd.cbDetails = (uint)Marshal.SizeOf(vol);
                mxcd.cChannels = 1;
                vol.value = volume;
                hmem = malloc(Marshal.SizeOf(vol));
                mxcd.paDetails = hmem;

                Marshal.StructureToPtr(vol, mxcd.paDetails, true);
                err = mixerSetControlDetails(hmixer, ref mxcd, 0x0);
                if (hmem != IntPtr.Zero)
                    free(hmem, Marshal.SizeOf(vol));
                return err;
            }
            catch { return err; }
        }
        #endregion
    }
}
