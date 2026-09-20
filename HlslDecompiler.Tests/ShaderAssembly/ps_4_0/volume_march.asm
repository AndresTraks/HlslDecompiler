ps_4_0
dcl_constantbuffer CB0[1], immediateIndexed
dcl_sampler s0, mode_default
dcl_resource_texture3d (float,float,float,float) t0
dcl_input_ps linear v1.xyz
dcl_input_ps linear v2.xyz
dcl_output o0
dcl_temps 4
dp3 r0.x, v2.xyz, v2.xyz
rsq r0.x, r0.x
mul r0.xyz, r0.xxx, v2.xyz
mov r1.xyz, v1.xyz
mov r2.xyz, l(0, 0, 0, 0)
mov r0.w, l(0)
mov r1.w, l(0)
loop
ige r2.w, r1.w, l(32)
breakc_nz r2.w
sample_l r3, r1.xyzx, t0, s0, l(0)
mul r2.w, r3.x, cb0[0].y
add r3.x, -r0.w, l(1)
mul r3.y, r2.w, r3.x
mad r3.yzw, r3.yyy, cb0[0].zzz, r2.xyz
mad r2.w, r2.w, r3.x, r0.w
lt r3.x, cb0[0].w, r2.w
if_nz r3.x
mov r2.xyz, r3.yzw
mov r0.w, r2.w
break
endif
mad r1.xyz, r0.xyz, cb0[0].xxx, r1.xyz
iadd r1.w, r1.w, l(1)
mov r2.xyz, r3.yzw
mov r0.w, r2.w
endloop
mov o0.xyz, r2.xyz
mov o0.w, r0.w
ret
