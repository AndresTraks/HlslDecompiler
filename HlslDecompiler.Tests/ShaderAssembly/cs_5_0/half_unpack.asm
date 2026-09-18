cs_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_structured t0, 4
dcl_uav_structured u0, 8
dcl_input vThreadID.x
dcl_temps 2
dcl_thread_group 64, 1, 1
ld_structured r0.x, vThreadID.x, l(0), t0.x
ushr r0.y, r0.x, l(16)
f16tof32 r1.xy, r0.xy
store_structured u0.xy, vThreadID.xx, l(0), r1.xy
ret
