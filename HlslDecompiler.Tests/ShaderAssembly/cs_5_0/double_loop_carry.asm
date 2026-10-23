cs_5_0
dcl_globalFlags refactoringAllowed | enableDoublePrecisionFloatOps
dcl_constantbuffer CB0[1], immediateIndexed
dcl_resource_structured t0, 8
dcl_uav_structured u0, 8
dcl_input vThreadID.x
dcl_temps 2
dcl_thread_group 64, 1, 1
dmov r0.xy, d(0.000000l, 0.000000l)
mov r0.z, l(0)
loop
uge r0.w, r0.z, cb0[0].z
breakc_nz r0.w
dmul r1.xy, r0.xy, cb0[0].xy
ld_structured_indexable(structured_buffer, stride=8)(mixed,mixed,mixed,mixed) r1.zw, r0.zz, l(0), t0.xy
dadd r0.xy, r1.zw, r1.xy
iadd r0.z, r0.z, l(1)
endloop
store_structured u0.xy, vThreadID.xx, l(0), r0.xy
ret
