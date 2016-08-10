/*
 * mono-hwcap-arm.c: ARM hardware feature detection
 *
 * Authors:
 *    Alex Rønne Petersen (alexrp@xamarin.com)
 *    Elijah Taylor (elijahtaylor@google.com)
 *    Miguel de Icaza (miguel@xamarin.com)
 *    Neale Ferguson (Neale.Ferguson@SoftwareAG-usa.com)
 *    Paolo Molaro (lupus@xamarin.com)
 *    Rodrigo Kumpera (kumpera@gmail.com)
 *    Sebastien Pouliot (sebastien@xamarin.com)
 *    Zoltan Varga (vargaz@xamarin.com)
 *
 * Copyright 2003 Ximian, Inc.
 * Copyright 2003-2011 Novell, Inc
 * Copyright 2006 Broadcom
 * Copyright 2007-2008 Andreas Faerber
 * Copyright 2011-2013 Xamarin Inc
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 */

#include "mono/utils/mono-hwcap-arm.h"

#if defined(HAVE_SYS_AUXV_H) && !defined(PLATFORM_ANDROID)
#include <sys/auxv.h>
#elif defined(__APPLE__)
#include <mach/machine.h>
#include <sys/sysctl.h>
#include <sys/types.h>
#else
#if defined (HAVE_SYS_UTSNAME_H)
#include <sys/utsname.h>
#endif
#include <stdio.h>
#endif

gboolean mono_hwcap_arm_is_v5 = FALSE;
gboolean mono_hwcap_arm_is_v6 = FALSE;
gboolean mono_hwcap_arm_is_v7 = FALSE;
gboolean mono_hwcap_arm_has_vfp = FALSE;
gboolean mono_hwcap_arm_has_vfp3 = FALSE;
gboolean mono_hwcap_arm_has_vfp3_d16 = FALSE;
gboolean mono_hwcap_arm_has_thumb = FALSE;
gboolean mono_hwcap_arm_has_thumb2 = FALSE;

void
mono_hwcap_arch_init (void)
{
#if defined(HAVE_SYS_AUXV_H) && !defined(PLATFORM_ANDROID)
	unsigned long hwcap;
	unsigned long platform;

	if ((hwcap = getauxval(AT_HWCAP))) {
		/* HWCAP_ARM_THUMB */
		if (hwcap & 0x00000004)
			mono_hwcap_arm_has_thumb = TRUE;

		/* HWCAP_ARM_VFP */
		if (hwcap & 0x00000040)
			mono_hwcap_arm_has_vfp = TRUE;

		/* HWCAP_ARM_VFPv3 */
		if (hwcap & 0x00002000)
			mono_hwcap_arm_has_vfp3 = TRUE;

		/* HWCAP_ARM_VFPv3D16 */
		if (hwcap & 0x00004000)
			mono_hwcap_arm_has_vfp3_d16 = TRUE;

		/* TODO: Find a way to detect Thumb 2. */
	}

	if ((platform = getauxval(AT_PLATFORM))) {
		const char *str = (const char *) platform;

		if (str [1] >= '5')
			mono_hwcap_arm_is_v5 = TRUE;

		if (str [1] >= '6')
			mono_hwcap_arm_is_v6 = TRUE;

		if (str [1] >= '7')
			mono_hwcap_arm_is_v7 = TRUE;

		/* TODO: Find a way to detect v7s. */
	}
#elif defined(__APPLE__)
	cpu_subtype_t sub_type;
	size_t length = sizeof (sub_type);

	sysctlbyname ("hw.cpusubtype", &sub_type, &length, NULL, 0);

	if (sub_type == CPU_SUBTYPE_ARM_V5TEJ || sub_type == CPU_SUBTYPE_ARM_XSCALE) {
		mono_hwcap_arm_is_v5 = TRUE;
	} else if (sub_type == CPU_SUBTYPE_ARM_V6) {
		mono_hwcap_arm_is_v5 = TRUE;
		mono_hwcap_arm_is_v6 = TRUE;
	} else if (sub_type == CPU_SUBTYPE_ARM_V7 || sub_type == CPU_SUBTYPE_ARM_V7F || sub_type == CPU_SUBTYPE_ARM_V7K) {
		mono_hwcap_arm_is_v5 = TRUE;
		mono_hwcap_arm_is_v6 = TRUE;
		mono_hwcap_arm_is_v7 = TRUE;
	}

	/* TODO: Find a way to detect features like Thumb and VFP. */
#else
	/* We can't use the auxiliary vector on Android due to
	 * permissions, so fall back to /proc/cpuinfo. We also
	 * hit this path if the target doesn't have sys/auxv.h.
	 */

#if defined (HAVE_SYS_UTSNAME_H)
	struct utsname name;

	/* Only fails if `name` is invalid (it isn't). */
	g_assert (!uname (&name));

	if (!strncmp (name.machine, "aarch64", 7) || !strncmp (name.machine, "armv8", 5)) {
		/*
		 * We're a 32-bit program running on an ARMv8 system.
		 * Whether the system is actually 32-bit or 64-bit
		 * doesn't matter to us. The important thing is that
		 * all 3 of ARMv8's execution states (A64, A32, T32)
		 * are guaranteed to have all of the features that
		 * we want to detect and use.
		 *
		 * We do this ARMv8 detection via uname () because
		 * in the early days of ARMv8 on Linux, the
		 * /proc/cpuinfo format was a disaster and there
		 * were multiple (merged into mainline) attempts at
		 * cleaning it up (read: breaking applications that
		 * tried to rely on it). So now multiple ARMv8
		 * systems in the wild have different /proc/cpuinfo
		 * output, some of which are downright useless.
		 *
		 * So, when it comes to detecting ARMv8 in a 32-bit
		 * program, it's better to just avoid /proc/cpuinfo
		 * entirely. Maybe in a decade or two, we won't
		 * have to worry about this mess that the Linux ARM
		 * maintainers created. One can hope.
		 */

		mono_hwcap_arm_is_v5 = TRUE;
		mono_hwcap_arm_is_v6 = TRUE;
		mono_hwcap_arm_is_v7 = TRUE;

		mono_hwcap_arm_has_vfp = TRUE;
		mono_hwcap_arm_has_vfp3 = TRUE;
		mono_hwcap_arm_has_vfp3_d16 = TRUE;

		mono_hwcap_arm_has_thumb = TRUE;
		mono_hwcap_arm_has_thumb2 = TRUE;
	}
#endif

	char buf [512];
	char *line;

	FILE *file = fopen ("/proc/cpuinfo", "r");

	if (file) {
		while ((line = fgets (buf, 512, file))) {
			if (!strncmp (line, "Processor", 9) ||
			    !strncmp (line, "model name", 10)) {
				char *ver = strstr (line, "(v");

				if (ver) {
					if (ver [2] >= '5')
						mono_hwcap_arm_is_v5 = TRUE;

					if (ver [2] >= '6')
						mono_hwcap_arm_is_v6 = TRUE;

					if (ver [2] >= '7')
						mono_hwcap_arm_is_v7 = TRUE;

					/* TODO: Find a way to detect v7s. */
				}

				continue;
			}

			if (!strncmp (line, "Features", 8)) {
				if (strstr (line, "thumb"))
					mono_hwcap_arm_has_thumb = TRUE;

				/* TODO: Find a way to detect Thumb 2. */

				if (strstr (line, "vfp"))
					mono_hwcap_arm_has_vfp = TRUE;

				if (strstr (line, "vfpv3"))
					mono_hwcap_arm_has_vfp3 = TRUE;

				if (strstr (line, "vfpv3-d16"))
					mono_hwcap_arm_has_vfp3_d16 = TRUE;

				continue;
			}
		}

		fclose (file);
	}
#endif
}

void
mono_hwcap_print(FILE *f)
{
	g_fprintf (f, "mono_hwcap_arm_is_v5 = %i\n", mono_hwcap_arm_is_v5);
	g_fprintf (f, "mono_hwcap_arm_is_v6 = %i\n", mono_hwcap_arm_is_v6);
	g_fprintf (f, "mono_hwcap_arm_is_v7 = %i\n", mono_hwcap_arm_is_v7);
	g_fprintf (f, "mono_hwcap_arm_has_vfp = %i\n", mono_hwcap_arm_has_vfp);
	g_fprintf (f, "mono_hwcap_arm_has_vfp3 = %i\n", mono_hwcap_arm_has_vfp3);
	g_fprintf (f, "mono_hwcap_arm_has_vfp3_d16 = %i\n", mono_hwcap_arm_has_vfp3_d16);
	g_fprintf (f, "mono_hwcap_arm_has_thumb = %i\n", mono_hwcap_arm_has_thumb);
	g_fprintf (f, "mono_hwcap_arm_has_thumb2 = %i\n", mono_hwcap_arm_has_thumb2);
}