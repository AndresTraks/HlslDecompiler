ps_5_0
dcl_globalFlags refactoringAllowed
dcl_uav_structured u1, 4
dcl_uav_typed_texture2d (float,float,float,float) u2
dcl_input_ps_siv linear noperspective v0.xy, position
dcl_input_ps constant v1.x
dcl_output o0
dcl_temps 2
ld_structured_indexable(structured_buffer, stride=4)(mixed,mixed,mixed,mixed) r0.x, v1.x, l(0), u1.x
mov o0.x, r0.x
store_structured u1.x, v1.x, l(0), l(0.000000)
ftou r0, v0.xyyy
ld_uav_typed_indexable(texture2d)(float,float,float,float) r1.x, r0.x, u2.x
mov o0.y, r1.x
store_uav_typed u2, r0, l(2, 2, 2, 2)
mov r0.y, l(0)
iadd r0.x, v1.x, l(1)
imm_atomic_iadd r1.x, u1, r0.xyxx, l(3)
ld_structured_indexable(structured_buffer, stride=4)(mixed,mixed,mixed,mixed) r0.x, r0.x, l(0), u1.x
utof o0.w, r0.x
utof o0.z, r1.x
ret
