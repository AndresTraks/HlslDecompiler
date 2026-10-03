hs_5_0
hs_decls
dcl_input_control_point_count 4
dcl_output_control_point_count 4
dcl_tessellator_domain domain_quad
dcl_tessellator_partitioning partitioning_fractional_odd
dcl_tessellator_output_primitive output_triangle_cw
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
hs_fork_phase
dcl_input vicp[4][0].x
dcl_output o0.y
dcl_temps 1
add r0.x, vicp[1][0].x, vicp[0][0].x
add r0.x, r0.x, vicp[2][0].x
add r0.x, r0.x, vicp[3][0].x
mul o0.y, r0.x, l(0.25)
ret
hs_fork_phase
dcl_input vicp[4][0].y
dcl_output o0.z
dcl_temps 1
add r0.x, vicp[1][0].y, vicp[0][0].y
add r0.x, r0.x, vicp[2][0].y
add r0.x, r0.x, vicp[3][0].y
mul o0.z, r0.x, l(0.25)
ret
hs_fork_phase
dcl_input vicp[4][0].z
dcl_output o0.w
dcl_temps 1
add r0.x, vicp[1][0].z, vicp[0][0].z
add r0.x, r0.x, vicp[2][0].z
add r0.x, r0.x, vicp[3][0].z
mul o0.w, r0.x, l(0.25)
ret
hs_join_phase
dcl_input vpc0.yzw
dcl_output_siv o0.x, finalQuadUeq0EdgeTessFactor
dcl_output_siv o1.x, finalQuadVeq0EdgeTessFactor
dcl_output_siv o2.x, finalQuadUeq1EdgeTessFactor
dcl_output_siv o3.x, finalQuadVeq1EdgeTessFactor
dcl_output_siv o4.x, finalQuadUInsideTessFactor
dcl_output_siv o5.x, finalQuadVInsideTessFactor
dcl_temps 1
add r0.xyz, -cb0[0].xyz, vpc0.yzw
dp3 r0.x, r0.xyz, r0.xyz
sqrt r0.x, r0.x
max r0.x, r0.x, l(0.00100000005)
div r0.x, cb0[0].w, r0.x
max r0.y, r0.x, l(1)
mul r0.x, r0.x, l(0.5)
max r0.x, r0.x, l(1)
min r0.xy, r0.xy, l(64, 64, 0, 0)
mov o0.x, r0.y
mov o1.x, r0.y
mov o2.x, r0.y
mov o3.x, r0.y
mov o4.x, r0.x
mov o5.x, r0.x
ret
