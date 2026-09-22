ps_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_texture2d (float,float,float,float) t0
dcl_resource_structured t1, 16
dcl_uav_typed_texture2d (float,float,float,float) u1
dcl_uav_typed_buffer (uint,uint,uint,uint) u2
dcl_input_ps_siv linear noperspective v0.xyw, position
dcl_output o0
dcl_temps 3
mov r0.zw, l(0, 0, 0, 0)
ftoi r0.xy, v0.xy
ld_indexable(texture2d)(float,float,float,float) r1, r0, t0
ld_uav_typed_indexable(texture2d)(float,float,float,float) r2, r0.xyyy, u1
add r1, r1, r2
ld_structured_indexable(structured_buffer, stride=16)(mixed,mixed,mixed,mixed) r0.z, r0.x, l(12), t1.x
utof r0.z, r0.z
add r1, r0.z, r1
ld_uav_typed_indexable(buffer)(uint,uint,uint,uint) r0.z, r0.y, u2.x
utof r0.z, r0.z
mad r1, r0.z, v0.w, r1
store_uav_typed u1, r0.xyyy, r1
mov o0, r1
ret
