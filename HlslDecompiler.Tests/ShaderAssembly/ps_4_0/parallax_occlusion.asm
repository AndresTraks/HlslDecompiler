ps_4_0
dcl_constantbuffer CB0[1], immediateIndexed
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_resource_texture2d (float,float,float,float) t1
dcl_input_ps linear v1.xy
dcl_input_ps linear v2.xyz
dcl_output o0
dcl_temps 5
dp3 r0.x, v2.xyz, v2.xyz
rsq r0.x, r0.x
mul r0.xyz, r0.xxx, v2.xyz
mul r0.xy, r0.xy, cb0[0].xx
max r0.z, |r0.z|, l(0.00100000005)
div r0.xy, r0.xy, r0.zz
div r0.xy, r0.xy, cb0[0].yy
deriv_rtx r0.zw, v1.xy
deriv_rty r1.xy, v1.xy
sample_d r2, v1.xyxx, t0, s0, r0.zwzz, r1.xyxx
div r1.z, l(1, 1, 1, 1), cb0[0].y
mov r2.yz, v1.xy
mov r1.w, l(1)
mov r3.x, r2.x
mov r2.w, l(0)
loop
ige r4.x, r2.w, l(16)
breakc_nz r4.x
ge r4.x, r3.x, r1.w
if_nz r4.x
break
endif
add r1.w, -r1.z, r1.w
add r2.yz, -r0.xy, r2.yz
sample_d r3, r2.yzyy, t0, s0, r0.zwzz, r1.xyxx
iadd r2.w, r2.w, l(1)
endloop
sample r0, r2.yzyy, t1, s0
mov_sat r1.w, r1.w
mul o0, r0, r1.w
ret
