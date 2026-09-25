hs_5_0
hs_decls
dcl_input_control_point_count 3
dcl_output_control_point_count 3
dcl_tessellator_domain domain_tri
dcl_tessellator_partitioning partitioning_fractional_odd
dcl_tessellator_output_primitive output_triangle_cw
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[5], immediateIndexed
hs_join_phase
dcl_input vicp[3][0].xyz
dcl_output_siv o0.x, finalTriUeq0EdgeTessFactor
dcl_output o0.yzw
dcl_output_siv o1.x, finalTriVeq0EdgeTessFactor
dcl_output_siv o2.x, finalTriWeq0EdgeTessFactor
dcl_output_siv o3.x, finalTriInsideTessFactor
dcl_temps 2
add r0.xyz, vicp[1][0].xyz, vicp[0][0].xyz
add r0.xyz, r0.xyz, vicp[2][0].xyz
mul r1.xyz, r0.xyz, l(0.333333343, 0.333333343, 0.333333343, 0)
dp3 r0.w, cb0[0].xyz, r1.xyz
add r0.w, r0.w, cb0[0].w
lt r0.w, l(-2), r0.w
dp3 r1.w, cb0[1].xyz, r1.xyz
add r1.w, r1.w, cb0[1].w
lt r1.w, l(-2), r1.w
and r0.w, r0.w, r1.w
dp3 r1.w, cb0[2].xyz, r1.xyz
add r1.w, r1.w, cb0[2].w
lt r1.w, l(-2), r1.w
and r0.w, r0.w, r1.w
dp3 r1.w, cb0[3].xyz, r1.xyz
add r1.w, r1.w, cb0[3].w
lt r1.w, l(-2), r1.w
and r0.w, r0.w, r1.w
if_z r0.w
mov o0.yzw, r1.xyz
mov o0.x, l(0)
mov o1.x, l(0)
mov o2.x, l(0)
mov o3.x, l(0)
ret
endif
mad r0.xyz, r0.xyz, l(0.333333343, 0.333333343, 0.333333343, 0), -cb0[4].xyz
dp3 r0.x, r0.xyz, r0.xyz
sqrt r0.x, r0.x
max r0.x, r0.x, l(0.00100000005)
div r0.x, cb0[4].w, r0.x
mov o0.yzw, r1.xyz
mov o0.x, r0.x
mov o1.x, r0.x
mov o2.x, r0.x
mov o3.x, r0.x
ret
