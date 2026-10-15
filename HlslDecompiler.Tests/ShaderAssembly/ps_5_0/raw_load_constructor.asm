ps_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_raw t0
dcl_input_ps_siv linear noperspective v0.x, position
dcl_output o0
dcl_temps 3
ftou r0.x, v0.x
ishl r0.x, r0.x, l(4)
iadd r0.y, r0.x, l(64)
ld_raw_indexable(raw_buffer)(mixed,mixed,mixed,mixed) r1, r0.x, t0
ld_raw_indexable(raw_buffer)(mixed,mixed,mixed,mixed) r0.xy, r0.yy, t0.xy
f16tof32 r2.z, r0.x
iadd r0.x, r1.w, r1.z
mov r2.xy, r1.xy
utof r2.w, r0.x
mul o0, r0.y, r2
ret
