cs_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_buffer (float,float,float,float) t0
dcl_resource_raw t1
dcl_resource_structured t2, 8
dcl_uav_structured u0, 16
dcl_input vThreadID.x
dcl_temps 2
dcl_thread_group 32, 1, 1
bufinfo_indexable(structured_buffer, stride=8)(mixed,mixed,mixed,mixed) r0.x, t2.x
ishl r0.x, r0.x, l(3)
utof r0.z, r0.x
bufinfo_indexable(raw_buffer)(mixed,mixed,mixed,mixed) r0.y, t1.x
bufinfo_indexable(buffer)(float,float,float,float) r1.x, t0.x
udiv null, r1.y, vThreadID.x, r1.x
utof r0.x, r1.x
ld_indexable(buffer)(float,float,float,float) r0.w, r1.y, t0.x
store_structured u0, vThreadID.x, l(0), r0
ret
