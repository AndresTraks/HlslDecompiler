ps_4_0
dcl_constantbuffer cb0[1], immediateIndexed
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_input_ps linear v0.xy
dcl_output o0
dcl_temps 3
mov r0, l(0, 0, 0, 0)
mov r1.x, l(0)
loop
ige r1.y, r1.x, l(8)
breakc_nz r1.y
itof r1.y, r1.x
mad r1.yz, cb0[0].zw, r1.yy, v0.xy
sample_l r2, r1.yzyy, t0, s0, l(0)
mad r1.yzw, r2.xyz, cb0[0].xxx, r0.xyz
lt r2.x, r2.x, cb0[0].y
if_nz r2.x
iadd r2.x, r1.x, l(1)
mov r0.xyz, r1.yzw
mov r1.x, r2.x
continue
endif
add r0.w, r0.w, l(1)
iadd r1.x, r1.x, l(1)
mov r0.xyz, r1.yzw
endloop
max r1.x, r0.w, l(1)
div o0.xyz, r0.xyz, r1.xxx
mov o0.w, r0.w
ret
