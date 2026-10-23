hs_5_0
hs_decls
dcl_input_control_point_count 4
dcl_output_control_point_count 4
dcl_tessellator_domain domain_quad
dcl_tessellator_partitioning partitioning_fractional_even
dcl_tessellator_output_primitive output_triangle_ccw
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
hs_fork_phase
dcl_hs_fork_phase_instance_count 4
dcl_input vForkInstanceID
dcl_input vicp[4][0].xyz
dcl_output_siv o0.x, finalQuadUeq0EdgeTessFactor
dcl_output_siv o1.x, finalQuadVeq0EdgeTessFactor
dcl_output_siv o2.x, finalQuadUeq1EdgeTessFactor
dcl_output_siv o3.x, finalQuadVeq1EdgeTessFactor
dcl_temps 1
dcl_indexrange o0 4
iadd r0.x, vForkInstanceID.x, l(1)
and r0.x, r0.x, l(3)
mov r0.y, vForkInstanceID.x
add r0.xzw, vicp[r0.x][0].xyz, vicp[r0.y][0].xyz
mad r0.xzw, r0.xzw, l(0.5, 0, 0.5, 0.5), -cb0[0].xyz
dp3 r0.x, r0.xzw, r0.xzw
sqrt r0.x, r0.x
div r0.x, cb0[0].w, r0.x
max r0.x, r0.x, l(1)
min r0.x, r0.x, l(16)
mov o[r0.y].x, r0.x
ret
hs_fork_phase
dcl_hs_fork_phase_instance_count 4
dcl_input vForkInstanceID
dcl_input vicp[4][0].x
dcl_output o6.x
dcl_output o7.x
dcl_output o8.x
dcl_output o9.x
dcl_temps 1
dcl_indexrange o6 4
mov r0.x, vForkInstanceID.x
add r0.y, -cb0[0].x, vicp[r0.x][0].x
mov o[r0.x + 6].x, r0.y
ret
hs_fork_phase
dcl_hs_fork_phase_instance_count 4
dcl_input vForkInstanceID
dcl_input vicp[4][0].y
dcl_output o6.y
dcl_output o7.y
dcl_output o8.y
dcl_output o9.y
dcl_temps 1
dcl_indexrange o6 4
mov r0.x, vForkInstanceID.x
add r0.y, -cb0[0].y, vicp[r0.x][0].y
mov o[r0.x + 6].y, r0.y
ret
hs_fork_phase
dcl_hs_fork_phase_instance_count 4
dcl_input vForkInstanceID
dcl_input vicp[4][0].z
dcl_output o6.z
dcl_output o7.z
dcl_output o8.z
dcl_output o9.z
dcl_temps 1
dcl_indexrange o6 4
mov r0.x, vForkInstanceID.x
add r0.y, -cb0[0].z, vicp[r0.x][0].z
mov o[r0.x + 6].z, r0.y
ret
hs_join_phase
dcl_hs_join_phase_instance_count 2
dcl_input vpc0.x
dcl_input vpc1.x
dcl_input vpc2.x
dcl_input vpc3.x
dcl_input vJoinInstanceID
dcl_output_siv o4.x, finalQuadUInsideTessFactor
dcl_output_siv o5.x, finalQuadVInsideTessFactor
dcl_temps 1
dcl_indexrange o4 2
dcl_indexrange vpc0 4
iadd r0.x, -vJoinInstanceID.x, l(1)
add r0.x, vpc[r0.x].x, vpc[r0.x + 2].x
mul r0.x, r0.x, l(0.5)
mov r0.y, vJoinInstanceID.x
mov o[r0.y + 4].x, r0.x
ret
hs_join_phase
dcl_input vpc0.x
dcl_input vpc1.x
dcl_input vpc2.x
dcl_input vpc3.x
dcl_output o0.y
dcl_temps 1
add r0.x, vpc0.x, vpc1.x
add r0.x, r0.x, vpc2.x
add r0.x, r0.x, vpc3.x
mul o0.y, r0.x, l(0.25)
ret
