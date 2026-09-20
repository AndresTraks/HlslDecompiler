ps_4_0
dcl_constantbuffer CB0[1], immediateIndexed
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_input_ps linear v0.xy
dcl_output o0
dcl_temps 5
mov r0, l(0, 0, 0, 0)
loop
ige r1.x, r0.w, l(4)
breakc_nz r1.x
itof r1.z, r0.w
mov r2.xyz, r0.xyz
mov r1.x, l(0)
loop
ige r1.w, r1.x, l(4)
breakc_nz r1.w
itof r1.y, r1.x
mad r3.yz, r1.yz, cb0[0].zw, v0.xy
sample_l r4, r3.yzyy, t0, s0, l(0)
max r3.x, r2.x, r4.x
lt r1.y, cb0[0].x, r4.x
if_nz r1.y
mov r2.xyz, r3.xyz
break
endif
iadd r1.x, r1.x, l(1)
mov r2.xyz, r3.xyz
endloop
lt r1.x, cb0[0].y, r2.x
if_nz r1.x
mov r0.xyz, r2.xyz
break
endif
iadd r0.w, r0.w, l(1)
mov r0.xyz, r2.xyz
endloop
mov o0.xyz, r0.yzx
mov o0.w, l(1)
ret
