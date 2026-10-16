ps_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_texture2darray (uint,uint,uint,uint) t0
dcl_resource_texture2dms(4) (sint,sint,sint,sint) t1
dcl_uav_typed_texture2d (uint,uint,uint,uint) u1
dcl_input_ps_siv linear noperspective v0.xy, position
dcl_input_ps constant v1.x
dcl_output o0
dcl_temps 3
ftou r0.xy, v0.yx
ld_uav_typed_indexable(texture2d)(uint,uint,uint,uint) r0.z, r0.x, u1.x
and r1.z, v1.x, l(3)
ftoi r1.xy, v0.xy
mov r1.w, l(0)
ld_aoffimmi_indexable(1,-1,0)(texture2darray)(uint,uint,uint,uint) r2.xyz, r1.xyz, t0.xyw
ldms_indexable(texture2dms)(sint,sint,sint,sint) r1.xy, r1.xy, t1.xy, r1.zz
atomic_umax u1, r0.xyxx, r2.x
xor o0.z, r0.z, r1.y
itof o0.y, r1.x
utof o0.w, r2.z
mov o0.x, r2.y
ret
