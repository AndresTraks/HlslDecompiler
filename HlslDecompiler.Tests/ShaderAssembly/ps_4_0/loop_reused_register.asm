ps_4_0
dcl_constantbuffer CB0[5], immediateIndexed
dcl_sampler s0, mode_comparison
dcl_sampler s1, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_resource_texture2d (float,float,float,float) t1
dcl_input_ps linear v1
dcl_input_ps linear v2.xy
dcl_output o0
dcl_temps 3
dp4 r0.x, cb0[3], v1
dp4 r1.x, v1, cb0[0]
dp4 r1.y, v1, cb0[1]
dp4 r1.z, v1, cb0[2]
div r0.xyz, r1.xyz, r0.xxx
mad r1.yz, r0.xy, l(0, 0.5, -0.5, 0), l(0, 0.5, 0.5, 0)
add r0.x, r0.z, -cb0[4].x
add r2.y, r1.z, -cb0[4].w
mov r0.yz, l(0, 0, -1, 0)
loop
ilt r0.w, l(1), r0.z
breakc_nz r0.w
itof r0.w, r0.z
mad r2.x, r0.w, cb0[4].z, r1.y
sample_c_lz r0.w, r2.x, t0.x, s0, r0.x
add r0.y, r0.w, r0.y
iadd r0.z, r0.z, l(1)
endloop
mov r0.z, r0.y
mov r0.w, l(-1)
loop
ilt r1.w, l(1), r0.w
breakc_nz r1.w
itof r1.w, r0.w
mad r1.x, r1.w, cb0[4].z, r1.y
sample_c_lz r1.x, r1.x, t0.x, s0, r0.x
add r0.z, r0.z, r1.x
iadd r0.w, r0.w, l(1)
endloop
add r2.y, r1.z, cb0[4].w
mov r0.y, r0.z
mov r0.w, l(-1)
loop
ilt r1.x, l(1), r0.w
breakc_nz r1.x
itof r1.x, r0.w
mad r2.x, r1.x, cb0[4].z, r1.y
sample_c_lz r1.x, r2.x, t0.x, s0, r0.x
add r0.y, r0.y, r1.x
iadd r0.w, r0.w, l(1)
endloop
sample r1, v2.xyxx, t1, s1
mul r0.x, r0.y, l(0.111111112)
mul o0, r1, r0.x
ret
