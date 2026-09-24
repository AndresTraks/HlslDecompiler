cs_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_buffer (float,float,float,float) t0
dcl_resource_structured t1, 16
dcl_resource_raw t2
dcl_uav_typed_buffer (float,float,float,float) u0
dcl_uav_structured u1, 16
dcl_input vThreadID.x
dcl_temps 1
dcl_thread_group 8, 1, 1
bufinfo_indexable(buffer)(float,float,float,float) r0.x, t0.x
bufinfo_indexable(structured_buffer, stride=16)(mixed,mixed,mixed,mixed) r0.y, t1.x
bufinfo_indexable(raw_buffer)(mixed,mixed,mixed,mixed) r0.z, t2.x
bufinfo_indexable(buffer)(float,float,float,float) r0.w, u0.x
store_structured u1, vThreadID.x, l(0), r0
ret
