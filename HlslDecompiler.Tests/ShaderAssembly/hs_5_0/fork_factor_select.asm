hs_5_0
hs_decls
dcl_input_control_point_count 3
dcl_output_control_point_count 3
dcl_tessellator_domain domain_tri
dcl_tessellator_partitioning partitioning_integer
dcl_tessellator_output_primitive output_triangle_cw
dcl_globalFlags refactoringAllowed
dcl_immediateConstantBuffer { { 0, 0, 0, 1 }, { 0, 0, 2, 0 }, { 0, 1, 1, 0 }, { 1, 0, 0, 0 }, { 0, 0, 0, 0 } }
hs_fork_phase
dcl_hs_fork_phase_instance_count 3
dcl_input vForkInstanceID
dcl_output_siv o0.x, finalTriUeq0EdgeTessFactor
dcl_output_siv o1.x, finalTriVeq0EdgeTessFactor
dcl_output_siv o2.x, finalTriWeq0EdgeTessFactor
dcl_temps 1
dcl_indexrange o0 3
mov r0.x, vForkInstanceID.x
mov o[r0.x].x, l(1)
ret
hs_fork_phase
dcl_output_siv o3.x, finalTriInsideTessFactor
mov o3.x, l(1)
ret
hs_fork_phase
dcl_hs_fork_phase_instance_count 4
dcl_input vForkInstanceID
dcl_output o4.x
dcl_output o5.x
dcl_output o6.x
dcl_output o7.x
dcl_temps 1
dcl_indexrange o4 4
mov r0.x, vForkInstanceID.x
mov o[r0.x + 4].x, icb[r0.x].w
ret
hs_fork_phase
dcl_hs_fork_phase_instance_count 5
dcl_input vForkInstanceID
dcl_output o0.y
dcl_output o4.y
dcl_output o5.y
dcl_output o6.y
dcl_output o7.y
dcl_temps 1
dcl_indexrange o0 8
ult r0.x, vForkInstanceID.x, l(2)
iadd r0.yz, vForkInstanceID.xx, l(0, 4, 3, 0)
udiv null, r0.y, r0.y, l(5)
movc r0.x, r0.x, r0.y, r0.z
mov r0.y, vForkInstanceID.x
mov o[r0.x].y, icb[r0.y].z
ret
hs_fork_phase
dcl_hs_fork_phase_instance_count 4
dcl_input vForkInstanceID
dcl_output o4.z
dcl_output o5.z
dcl_output o6.z
dcl_output o7.z
dcl_temps 1
dcl_indexrange o4 4
mov r0.x, vForkInstanceID.x
mov o[r0.x + 4].z, icb[r0.x].y
ret
hs_fork_phase
dcl_hs_fork_phase_instance_count 4
dcl_input vForkInstanceID
dcl_output o4.w
dcl_output o5.w
dcl_output o6.w
dcl_output o7.w
dcl_temps 1
dcl_indexrange o4 4
mov r0.x, vForkInstanceID.x
mov o[r0.x + 4].w, icb[r0.x].x
ret
