cs_4_1
dcl_globalFlags refactoringAllowed
dcl_constantbuffer cb0[1], immediateIndexed
dcl_uav_structured u0, 4
dcl_input vThreadID.x
dcl_temps 1
dcl_thread_group 32, 1, 1
imul null, r0.x, vThreadID.x, cb0[0].y
imad r0.y, vThreadID.x, cb0[0].y, cb0[0].y
ult r0.z, r0.y, cb0[0].x
if_nz r0.z
ld_structured r0.z, r0.y, l(0), u0.x
ld_structured r0.x, r0.x, l(0), u0.x
iadd r0.x, r0.x, r0.z
store_structured u0.x, r0.y, l(0), r0.x
endif
ret
