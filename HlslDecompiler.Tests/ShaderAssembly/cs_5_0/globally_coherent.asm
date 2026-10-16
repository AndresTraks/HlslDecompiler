cs_5_0
dcl_globalFlags refactoringAllowed
dcl_uav_structured_glc_opc u0, 4
dcl_uav_raw_glc u1
dcl_uav_typed_buffer_glc (uint,uint,uint,uint) u2
dcl_input vThreadID.x
dcl_temps 1
dcl_thread_group 8, 1, 1
imm_atomic_alloc r0.x, u0
store_structured u0.x, r0.x, l(0), vThreadID.x
ld_uav_typed_indexable(buffer)(uint,uint,uint,uint) r0.x, vThreadID.x, u2.x
ishl r0.y, vThreadID.x, l(2)
store_raw u1.x, r0.y, r0.x
atomic_iadd u2, l(0), l(1)
ret
