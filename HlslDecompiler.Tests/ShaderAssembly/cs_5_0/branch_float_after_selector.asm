cs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_resource_buffer (float,float,float,float) t0
dcl_uav_structured u0, 4
dcl_input vThreadID.x
dcl_temps 1
dcl_thread_group 64, 1, 1
iadd r0.x, vThreadID.x, cb0[0].x
and r0.x, r0.x, l(7)
ult r0.y, l(3), r0.x
if_nz r0.y
ld_indexable(buffer)(float,float,float,float) r0.y, r0.x, t0.x
ieq r0.x, r0.x, l(5)
movc r0.x, r0.x, l(3), r0.y
else
mov r0.x, l(2)
endif
store_structured u0.x, vThreadID.x, l(0), r0.x
ret
