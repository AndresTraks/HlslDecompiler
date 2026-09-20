cs_4_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_resource_raw t0
dcl_uav_raw u0
dcl_input vThreadID.x
dcl_temps 3
dcl_thread_group 64, 1, 1
uge r0.x, vThreadID.x, cb0[0].x
if_nz r0.x
ret
endif
ishl r0.x, vThreadID.x, l(2)
ld_raw r1.x, r0.x, t0.x
ushr r2.x, r1.x, l(24)
ushr r2.y, r1.x, l(16)
ushr r2.z, r1.x, l(8)
and r0.yz, r2.yz, l(0, 255, 255, 0)
imul null, r0.y, r0.y, l(151)
imad r0.y, r2.x, l(77), r0.y
imad r0.y, r0.z, l(28), r0.y
ushr r0.z, r0.y, l(8)
ishl r0.w, r0.z, l(24)
ishl r0.z, r0.z, l(16)
iadd r0.z, r0.z, r0.w
and r0.y, r0.y, l(65280)
iadd r0.y, r0.y, r0.z
and r0.z, r1.x, l(255)
iadd r0.y, r0.z, r0.y
store_raw u0.x, r0.x, r0.y
ret
