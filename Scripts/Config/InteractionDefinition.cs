using System;
using System.Collections.Generic;
using ActRoguelike.Interaction;
using Godot;

namespace ActRoguelike.Config;

/// <summary>交互后端与外观可分别替换；新增后端不要求增加InteractionKind分支。</summary>
[Tool, GlobalClass]
public partial class InteractionDefinition : Resource, IContentValidatable
{
	[Export] public PackedScene? BackendScene { get; set; }
	[Export] public PackedScene? ModelScene { get; set; }
	[Export] public InteractionKind VisualStyle { get; set; } = InteractionKind.Signal;
	public IEnumerable<string> Validate()
	{
		if (BackendScene == null) { yield return "BackendScene：必须指定交互后端场景。"; yield break; }
		Node backend = BackendScene.Instantiate(); bool validBackend = backend is Interactable; backend.Free();
		if (!validBackend) yield return "BackendScene：根节点必须继承Interactable。";
		if (ModelScene != null) { Node model = ModelScene.Instantiate(); bool validModel = model is Node3D; model.Free(); if (!validModel) yield return "ModelScene：根节点必须是Node3D。"; }
		else if (VisualStyle == InteractionKind.None || !Enum.IsDefined(VisualStyle)) yield return "VisualStyle：没有自定义模型时必须选择有效外观。";
	}
	public Interactable? CreateBackend(InteractionComponent component)
	{
		if (BackendScene == null) return null;
		Node node = BackendScene.Instantiate();
		if (node is not Interactable backend) { node.Free(); throw new InvalidOperationException("交互后端根节点必须继承Interactable。"); }
		if (backend is IInteractionConfigurationConsumer consumer) consumer.ConfigureInteraction(component);
		return backend;
	}
	public Node3D CreateModel(CharacterConfig? character, bool preview = false)
	{
		if (ModelScene == null) return InteractionModel.Create(VisualStyle, character, preview);
		Node node = ModelScene.Instantiate();
		if (node is Node3D model) { model.Name = "TypeModel"; return model; }
		node.Free(); throw new InvalidOperationException("交互模型根节点必须是Node3D。");
	}
}
